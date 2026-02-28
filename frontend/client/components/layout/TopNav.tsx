import { Bell, UtensilsCrossed } from "lucide-react";
import { useLocation } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { api } from "@/lib/api";

const titles: Record<string, string> = {
  "/": "FoodTrack",
  "/pantry": "My Pantry",
  "/scan": "Scanner",
  "/recipes": "Recipes",
  "/recipes/": "Recipes",
  "/profile": "My Profile",
};

export function TopNav() {
  const location = useLocation();
  const [open, setOpen] = useState(false);
  const currentTitle = titles[location.pathname] || "FoodTrack";
  const { token } = useAuth();

  const notificationsQuery = useQuery({
    queryKey: ["notifications"],
    queryFn: () => api.notifications(token!),
    enabled: !!token && open,
  });

  return (
    <header className="sticky top-0 z-40 w-full bg-background/80 backdrop-blur-lg border-b border-border px-6 h-20 flex items-end pb-4 justify-between">
      <div className="flex items-center gap-2">
        <UtensilsCrossed className="h-6 w-6 text-primary" />
        <h1 className="text-xl font-bold text-foreground tracking-tight">{currentTitle}</h1>
      </div>

      <div className="relative">
        <Button variant="ghost" size="icon" className="rounded-full h-10 w-10 relative" onClick={() => setOpen((v) => !v)}>
          <Bell className="h-5 w-5 text-muted-foreground" />
          <span className="absolute top-2.5 right-2.5 w-2 h-2 bg-red-500 rounded-full border-2 border-background"></span>
        </Button>

        {open && (
          <div className="absolute right-0 mt-2 w-80 max-h-80 overflow-auto rounded-2xl border border-border bg-white dark:bg-card shadow-xl p-3 z-50">
            <p className="text-sm font-bold mb-2">Notifications</p>
            {notificationsQuery.isLoading && <p className="text-xs text-muted-foreground">Loading...</p>}
            {!notificationsQuery.isLoading && (notificationsQuery.data?.length ?? 0) === 0 && (
              <p className="text-xs text-muted-foreground">No notifications yet.</p>
            )}
            <div className="flex flex-col gap-2">
              {(notificationsQuery.data ?? []).map((n) => (
                <div key={n.id} className="rounded-xl border p-2 text-xs">
                  <p className="font-semibold">{n.notificationType}</p>
                  <p className="text-muted-foreground">{new Date(n.sentAtUtc).toLocaleString()}</p>
                  <p className={n.success ? "text-green-600" : "text-red-600"}>{n.success ? "Sent" : n.errorMessage ?? "Failed"}</p>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </header>
  );
}
