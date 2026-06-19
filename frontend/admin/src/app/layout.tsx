import type { Metadata } from "next";
import "./globals.css";
import { AuthProvider } from "@/contexts/AuthContext";
import { ToastProvider } from "@/components/toast";
import { GlobalUploadProvider } from "@/components/ui/GlobalUploadManager";

export const metadata: Metadata = {
  title: "Monopoluzem",
  description: "Monopol Uzaktan Eğitim Platformu",
  icons: {
    icon: "/monopolfavicon.png",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="tr">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&family=Outfit:wght@400;500;600;700&display=swap" rel="stylesheet" />
      </head>
      <body className="bg-[#E2E8F0]/20 text-[#0A1931] antialiased">
        <AuthProvider>
          <GlobalUploadProvider>
            <ToastProvider>
              {children}
            </ToastProvider>
          </GlobalUploadProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
