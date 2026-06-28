"use client";

import { useState, useEffect, FormEvent } from "react";
import { useRouter } from "next/navigation";
import Image from "next/image";
import { useAuth } from "@/contexts/AuthContext";
import { tenantApi, TenantBrandingDto } from "@/lib/api";
import { Eye, EyeOff } from "lucide-react";

export default function AdminLoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
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
      const role = user.role?.toLowerCase();
      if (role === "student") {
        // Öğrenci yanlışlıkla admin panelinden giriş yaptıysa onu öğrenci paneline yönlendir
        const t = localStorage.getItem("muro_token");
        const r = localStorage.getItem("muro_refresh");
        if (t) localStorage.setItem("muro_student_token", t);
        if (r) localStorage.setItem("muro_student_refresh", r);
        
        localStorage.removeItem("muro_token");
        localStorage.removeItem("muro_refresh");

        window.location.href = "/"; // Öğrenci portalının adresi
      } else {
        router.push("/dashboard");
      }
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
        setError("Bağlantı hatası. Lütfen ağınızı kontrol edin.");
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
      <div className="w-full max-w-md px-4 py-8 sm:p-6">

        {/* Logo */}
        <div className="text-center mb-6 sm:mb-8">
          <img 
            src="/icon.png" 
            alt={brandName} 
            className="w-48 sm:w-64 max-w-[80%] h-auto mx-auto object-contain drop-shadow-lg transition-all" 
          />
        </div>

        {/* Card */}
        <div className="bg-[#1B3B6F]/15 backdrop-blur-xl border border-[#1B3B6F]/30 rounded-2xl p-5 sm:p-8 shadow-2xl mx-2 sm:mx-0">
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
              <p className="mt-1.5 text-[11px] text-[#A0AEC0]/70 italic leading-relaxed">
                * Kullanıcı adınızı girerken lütfen Türkçe karakter kullanmayınız. <br />
                Örnek: İsim Soyisim Çağrı Özüşen, Kullanıcı Adı: cagriozusen
              </p>
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
      </div>
    </div>
  );
}
