import * as React from "react";
import { BottomNav } from "./BottomNav";
import { TopNav } from "./TopNav";
import { useLocation } from "react-router-dom";

const NO_NAV_ROUTES = ["/splash", "/onboarding", "/login", "/register"];

export function MobileLayout({ children }: { children: React.ReactNode }) {
  const location = useLocation();
  const pathname = location.pathname;
  const showNav = !NO_NAV_ROUTES.includes(pathname);

  return (
    <div className="min-h-screen bg-muted/20 flex justify-center items-center overflow-x-hidden p-0 md:p-8">
      {/* iPhone 14 frame for desktop, full viewport on mobile */}
      <div className="relative w-full h-screen bg-background shadow-2xl flex flex-col md:max-w-[390px] md:h-[844px] md:rounded-[3rem] md:border-[8px] md:border-foreground/90 md:overflow-hidden overflow-hidden">
        
        {/* Notch simulation - Desktop only */}
        <div className="hidden md:block absolute top-0 left-1/2 -translate-x-1/2 w-[120px] h-[30px] bg-foreground/90 rounded-b-[1.5rem] z-50"></div>

        {/* Top Navbar */}
        {showNav && <TopNav />}

        {/* Scrollable Main Content */}
        <main className="flex-1 overflow-y-auto custom-scrollbar relative z-10 w-full">
          {children}
        </main>
        
        {/* Bottom Navbar */}
        {showNav && <BottomNav />}
      </div>
    </div>
  );
}
