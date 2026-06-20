"use client";

import { useState, useEffect, FormEvent } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { tenantApi, type TenantBrandingDto } from "@/lib/api";
import { heavyTap } from "@/hooks/useHaptics";
import { Eye, EyeOff } from "lucide-react";

const DEV_MODE = process.env.NEXT_PUBLIC_DEV_AUTOLOGIN === "true";

export default function StudentLoginPage() {
  const [email, setEmail] = useState(DEV_MODE ? (process.env.NEXT_PUBLIC_DEV_EMAIL || "ogrenci@demo.com") : "");
  const [password, setPassword] = useState(DEV_MODE ? (process.env.NEXT_PUBLIC_DEV_PASSWORD || "123456") : "");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [branding, setBranding] = useState<TenantBrandingDto | null>(null);
  const { login, isLoading: authLoading, user } = useAuth();
  const router = useRouter();

  // Tenant branding yükle
  useEffect(() => {
    tenantApi.getBranding().then(setBranding).catch(() => { });
  }, []);

  // Giriş yapılmışsa role göre yönlendir
  useEffect(() => {
    if (!authLoading && user) {
      const role = user.role?.toLowerCase();
      if (role === "admin" || role === "superadmin" || role === "teacher" || role === "instructor" || role === "assistant" || role === "accountant") {
        // Token'ları admin panelin beklediği key'lere de kaydet
        const t = localStorage.getItem("muro_student_token");
        const r = localStorage.getItem("muro_student_refresh");
        if (t) localStorage.setItem("muro_token", t);
        if (r) localStorage.setItem("muro_refresh", r);

        const isDev = window.location.hostname === "localhost";
        if (isDev) {
          // Dev: farklı port, token'ı URL ile aktar (localStorage paylaşılmıyor)
          window.location.href = `http://localhost:3001/admin/dashboard?_token=${encodeURIComponent(t || "")}&_refresh=${encodeURIComponent(r || "")}`;
        } else {
          // Production: aynı domain, localStorage paylaşılıyor — direkt yönlendir
          window.location.href = "/admin/dashboard";
        }
      } else {
        router.push("/dashboard");
      }
    }
  }, [user, authLoading, router]);

  const brandName = branding?.name || "MURO";
  const brandInitial = brandName.charAt(0).toUpperCase();
  const primaryColor = branding?.primaryColor || "#1B3B6F";

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    setIsLoading(true);
    try {
      await login(email, password);
    } catch (err) {
      heavyTap();
      const msg = err instanceof Error ? err.message : "Giriş başarısız.";
      if (msg.toLowerCase().includes("fetch") || msg.toLowerCase().includes("network")) {
        setError("Sunucuya bağlanılamıyor. Lütfen API'nin çalıştığını kontrol edin.");
      } else {
        setError(msg);
      }
      setIsLoading(false);
      return;
    }
    // Role-based redirect handled in useEffect below
  };

  // Auth context yükleniyorsa ya da dev mode auto-login deneniyorsa spinner göster
  if (authLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#060E1A] via-[#0A1931] to-[#060E1A]">
        <div className="flex flex-col items-center gap-4">
          <div className="w-12 h-12 border-2 border-[#1B3B6F] border-t-transparent rounded-full animate-spin" />
          {DEV_MODE && (
            <p className="text-[#A0AEC0] text-sm animate-pulse">Geliştirici modu — otomatik giriş yapılıyor...</p>
          )}
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#060E1A] via-[#0A1931] to-[#060E1A]">
      <div className="w-full max-w-md p-6">

        {/* Dev mode banner */}
        {DEV_MODE && (
          <div className="mb-5 px-4 py-3 rounded-xl bg-amber-500/15 border border-amber-500/30 flex items-center gap-3">
            <span className="text-xl">🛠️</span>
            <div>
              <p className="text-amber-300 text-xs font-semibold">Geliştirici Modu Aktif</p>
              <p className="text-amber-400/70 text-xs mt-0.5">Demo hesap bilgileri otomatik dolduruldu</p>
            </div>
          </div>
        )}

        {/* Logo — tenant branding */}
        <div className="text-center mb-8">
          <img 
            src="/monopol_logo.png" 
            alt={brandName} 
            className="w-64 h-auto mx-auto object-contain drop-shadow-lg" 
          />
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
              <div className="relative">
                <input
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  required
                  className="w-full px-4 py-3 pr-12 bg-[#1B3B6F]/15 border border-[#1B3B6F]/30 rounded-xl text-white placeholder-[#A9A9A9] focus:outline-none focus:ring-2 focus:ring-[#1B3B6F] focus:border-transparent transition-all text-sm"
                  placeholder="••••••••"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-[#A0AEC0] hover:text-white transition-colors flex items-center justify-center p-1"
                >
                  {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
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

        <p className="text-center text-[#A0AEC0] text-xs mt-6">
          Hesabınız yok mu? Kurumunuzla iletişime geçin.
        </p>
      </div>
    </div>
  );
}
