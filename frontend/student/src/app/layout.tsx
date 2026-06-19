import type { Metadata } from "next";
import "./globals.css";
import "plyr/dist/plyr.css";
import { AuthProvider } from "@/contexts/AuthContext";
import { ToastProvider } from "@/components/ToastProvider";
import OfflineIndicator from "@/components/OfflineIndicator";
import NativeFeatures from "@/components/NativeFeatures";

export const metadata: Metadata = {
  title: "Monopoluzem",
  description: "Monopol Uzaktan Eğitim Platformu",
  icons: {
    icon: "/monopolfavicon.png",
  },
  viewport: "width=device-width, initial-scale=1.0, viewport-fit=cover, maximum-scale=1.0, user-scalable=no",
  other: {
    "mobile-web-app-capable": "yes",
    "apple-mobile-web-app-capable": "yes",
    "apple-mobile-web-app-status-bar-style": "black-translucent",
  },
  themeColor: "#0A1931",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="tr">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&family=Outfit:wght@400;500;600;700&display=swap" rel="stylesheet" />
      </head>
      <body className="bg-[#E2E8F0]/20 text-[#0A1931] antialiased">
        <AuthProvider>
          <ToastProvider>
            <NativeFeatures />
            <OfflineIndicator />
            {children}
          </ToastProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
