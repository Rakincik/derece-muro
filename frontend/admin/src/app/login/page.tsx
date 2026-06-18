"use client";

import { useState, useEffect, FormEvent } from "react";
import { useRouter } from "next/navigation";
import Image from "next/image";
import { useAuth } from "@/contexts/AuthContext";
import { tenantApi, TenantBrandingDto } from "@/lib/api";

export default function AdminLoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [branding, setBranding] = useState<TenantBrandingDto | null>(null);
  const { login, isLoading: authLoading, user } = useAuth();
  const router = useRouter();

  useEffect(() => {
    tenantApi.getBranding()
      .then(setBranding)
      .catch(() => { /* fallback to default */ });
  }, []);

  useEffect(() => {
    if (!authLoading && user) {
      router.push("/dashboard");
    }
  }, [user, authLoading, router]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setIsLoading(true);
    try {
      await login(email, password);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Giriş başarısız.";
      if (msg.toLowerCase().includes("fetch") || msg.toLowerCase().includes("network")) {
        setError("Sunucuya bağlanılamıyor. API'nin çalıştığını kontrol edin.");
      } else {
        setError(msg);
      }
      setIsLoading(false);
    }
  };

  if (authLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#060E1A] via-[#0A1931] to-[#060E1A]">
        <div className="flex flex-col items-center gap-4">
          <div className="w-12 h-12 border-2 border-[#1B3B6F] border-t-transparent rounded-full animate-spin" />
          <p className="text-[#A0AEC0] text-sm">Yükleniyor...</p>
        </div>
      </div>
    );
  }

  const brandName = branding?.name || "MURO";
  const brandInitial = brandName.charAt(0).toUpperCase();

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#060E1A] via-[#0A1931] to-[#060E1A]">
      <div className="w-full max-w-md p-6">

        {/* Logo */}
        <div className="text-center mb-8">
          <Image 
            src="/monopol_logo.png" 
            alt={brandName} 
            width={80} 
            height={80} 
            className="w-20 h-20 rounded-2xl mx-auto mb-4 object-contain shadow-2xl shadow-[#0A1931]/40" 
            priority 
          />
          <h1 className="text-3xl font-bold bg-gradient-to-r from-[#A0AEC0] via-[#E2E8F0] to-[#A9A9A9] bg-clip-text text-transparent">
            {brandName}
          </h1>
          <p className="text-[#A0AEC0] text-xs mt-2">Yönetim Paneli</p>
        </div>

        {/* Card */}
        <div className="bg-[#1B3B6F]/15 backdrop-blur-xl border border-[#1B3B6F]/30 rounded-2xl p-8 shadow-2xl">
          <h2 className="text-lg font-semibold text-white mb-6">Giriş Yap</h2>

          {error && (
            <div className="bg-red-500/10 border border-red-500/30 text-red-300 px-4 py-3 rounded-xl text-sm mb-5 flex items-start gap-2">
              <span className="text-base mt-0.5 flex-shrink-0">⚠️</span>
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-[#A0AEC0] mb-1.5 uppercase tracking-wide">Kullanıcı Adı</label>
              <input
                type="text"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                className="w-full px-4 py-3 bg-[#1B3B6F]/15 border border-[#1B3B6F]/30 rounded-xl text-white placeholder-[#A9A9A9] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F] focus:border-transparent transition-all text-sm"
                placeholder="Kullanıcı adınızı girin"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-[#A0AEC0] mb-1.5 uppercase tracking-wide">Şifre</label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                className="w-full px-4 py-3 bg-[#1B3B6F]/15 border border-[#1B3B6F]/30 rounded-xl text-white placeholder-[#A9A9A9] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F] focus:border-transparent transition-all text-sm"
                placeholder="••••••••"
              />
            </div>
            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-3 bg-gradient-to-r from-[#1B3B6F] to-[#0A1931] hover:from-[#1B3B6F]/90 hover:to-[#0A1931]/90 text-white font-semibold rounded-xl transition-all duration-200 hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:scale-100 shadow-lg shadow-[#0A1931]/40 text-sm mt-2"
            >
              {isLoading ? (
                <span className="flex items-center justify-center gap-2">
                  <span className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                  Giriş yapılıyor...
                </span>
              ) : "Giriş Yap →"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
