import { Link, useLocation } from "react-router-dom";
import { Home, ClipboardList, Scan, Utensils, User } from "lucide-react";
import { cn } from "@/lib/utils";

const NavItem = ({ to, icon: Icon, label, active }: { to: string, icon: any, label: string, active: boolean }) => (
  <Link
    to={to}
    className={cn(
      "flex flex-col items-center justify-center gap-1 py-1 px-1 transition-colors",
      active ? "text-primary" : "text-muted-foreground hover:text-foreground"
    )}
  >
    <Icon className={cn("h-6 w-6", active && "fill-primary/10")} />
    <span className="text-[10px] font-bold leading-none">{label}</span>
  </Link>
);

export function BottomNav() {
  const location = useLocation();
  const pathname = location.pathname;

  return (
    <nav className="z-50 bg-card/95 backdrop-blur-lg border-t border-border flex items-center justify-around px-2 pb-safe-area-inset-bottom h-16 w-full flex-shrink-0 relative">
      <NavItem to="/" icon={Home} label="Home" active={pathname === "/"} />
      <NavItem to="/pantry" icon={ClipboardList} label="Pantry" active={pathname === "/pantry"} />
      <div className="relative -top-5 z-50">
        <Link
          to="/scan"
          className="flex items-center justify-center w-14 h-14 bg-primary text-primary-foreground rounded-full shadow-lg shadow-primary/30 active:scale-95 transition-transform"
        >
          <Scan className="h-7 w-7" />
        </Link>
      </div>
      <NavItem to="/recipes" icon={Utensils} label="Recipes" active={pathname === "/recipes"} />
      <NavItem to="/profile" icon={User} label="Profile" active={pathname === "/profile"} />
    </nav>
  );
}
