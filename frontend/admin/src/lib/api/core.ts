export const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5292/api/v1";

export interface FetchOptions extends RequestInit {
    token?: string;
    tenantId?: string;
}

const _cache = new Map<string, { data: unknown; expiry: number }>();
const _inflight = new Map<string, Promise<unknown>>();

export class ApiError extends Error {
    constructor(message: string, public statusCode: number) {
        super(message);
        this.name = "ApiError";
    }
}

export async function api<T = unknown>(
    endpoint: string,
    options: FetchOptions = {}
): Promise<T> {
    const { token, tenantId, headers: customHeaders, ...rest } = options;

    const headers: Record<string, string> = {
        "Content-Type": "application/json",
        ...(customHeaders as Record<string, string>),
    };

    let finalToken = token;
    let finalTenantId = tenantId;

    if (typeof window !== "undefined") {
        if (!finalToken) finalToken = localStorage.getItem("muro_token") || undefined;
        if (!finalTenantId) finalTenantId = localStorage.getItem("muro_tenantId") || undefined;
    }

    if (finalToken) headers["Authorization"] = `Bearer ${finalToken}`;
    if (finalTenantId) headers["X-Tenant-Id"] = finalTenantId;

    const response = await fetch(`${API_URL}${endpoint}`, {
        cache: "no-store",
        ...rest,
        headers,
        credentials: "include",
    });

    if (!response.ok) {
        let errorData;
        let textContent = "";
        try {
            textContent = await response.text();
            errorData = JSON.parse(textContent);
            console.error(`API Error: ${response.status} | URL: ${endpoint} | Body:`, errorData);
        } catch {
            errorData = { message: textContent };
            console.error(`API Error: ${response.status} | URL: ${endpoint} | Text:`, textContent);
        }
        
        let errMsg = errorData.error
            || errorData.message
            || (errorData.errors ? Object.values(errorData.errors).flat().join(", ") : null)
            || errorData.title
            || `HTTP ${response.status}`;

        if (typeof errMsg === "string" && (errMsg.trim().startsWith("<") || errMsg.toLowerCase().includes("<html"))) {
            errMsg = "Sunucu ile bağlantı kurulamadı. Lütfen birazdan tekrar deneyin.";
        }

        if (response.status === 401 && errorData?.error === "SESSION_KICKED") {
            const displayMsg = errorData.message || errMsg;
            if (typeof window !== "undefined")
                window.dispatchEvent(new CustomEvent("session:kicked", { detail: { message: displayMsg, token: finalToken } }));
        }
        
        throw new ApiError(errMsg, response.status);
    }

    const method = (rest.method || "GET").toUpperCase();
    if (["POST", "PUT", "DELETE", "PATCH"].includes(method)) {
        _cache.clear();
    }

    if (response.status === 204) return undefined as T;
    
    const text = await response.text();
    if (!text) return undefined as T;
    
    try {
        return JSON.parse(text);
    } catch {
        return text as T;
    }
}

export async function cachedApi<T>(key: string, fetcher: () => Promise<T>, ttlMs = 300_000): Promise<T> {
    const cached = _cache.get(key);
    if (cached && cached.expiry > Date.now()) return cached.data as T;

    const existing = _inflight.get(key);
    if (existing) return existing as Promise<T>;

    const promise = fetcher().then(data => {
        _cache.set(key, { data, expiry: Date.now() + ttlMs });
        _inflight.delete(key);
        return data;
    }).catch(err => {
        _inflight.delete(key);
        throw err;
    });

    _inflight.set(key, promise);
    return promise;
}

export function invalidateCache(key: string) { _cache.delete(key); }
export function invalidateCacheByPrefix(prefix: string) {
    for (const key of _cache.keys()) {
        if (key.startsWith(prefix)) _cache.delete(key);
    }
}
export function clearCache() { _cache.clear(); }

// Common Interfaces
export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
