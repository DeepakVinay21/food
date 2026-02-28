import { useState, useMemo } from "react";
import { ChevronRight, Clock, Utensils, ChefHat, Sparkles, Search, Plus, ScanLine, ArrowRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Link, useNavigate } from "react-router-dom";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/components/auth/AuthProvider";

const SummaryCard = ({ title, count, color, bg }: { title: string; count: number; color: string; bg: string }) => (
  <div className={cn("p-3 sm:p-4 rounded-2xl flex flex-col items-center justify-center gap-1 shadow-sm text-center w-full h-full", bg)}>
    <span className={cn("text-[10px] font-bold uppercase tracking-wider opacity-80 leading-tight", color)}>{title}</span>
    <span className={cn("text-2xl font-black mt-auto", color)}>{count}</span>
  </div>
);

const ExpiringProductCard = ({ name, expiry, days, color, image }: { name: string; expiry: string; days: number; color: string; image: string }) => (
  <div className="min-w-[160px] bg-card rounded-2xl p-3 border border-border shadow-sm flex flex-col gap-2">
    <div className="w-full aspect-square rounded-xl bg-muted overflow-hidden flex items-center justify-center bg-primary/5">
      <img src={image} alt={name} className="w-full h-full object-cover" />
    </div>
    <div className="flex flex-col gap-0.5">
      <h3 className="font-semibold text-sm truncate">{name}</h3>
      <p className="text-[10px] text-muted-foreground flex items-center gap-1">
        <Clock className="h-3 w-3" />
        Exp: {expiry}
      </p>
    </div>
    <div className="mt-auto">
      <Badge variant="outline" className={cn("text-[10px] py-0 px-2 h-5", color)}>
        {days <= 0 ? "Expired" : days === 0 ? "Expires Today" : `Expires in ${days} days`}
      </Badge>
    </div>
  </div>
);

const SearchResultItem = ({ item }: { item: { name: string; expiry: string; days: number; status: string } }) => {
  const color = item.status === "expired" ? "text-red-500 bg-red-50" : item.status === "expiring" ? "text-orange-500 bg-orange-50" : "text-primary bg-primary/10";

  return (
    <div className="bg-card p-3 rounded-2xl border border-border flex items-center justify-between shadow-sm">
      <div className="flex items-center gap-3">
        <div className={cn("w-10 h-10 rounded-xl flex items-center justify-center", color)}>
          <Utensils className="h-5 w-5" />
        </div>
        <div className="flex flex-col">
          <span className="font-bold text-sm">{item.name}</span>
          <span className="text-xs text-muted-foreground">Expires {item.expiry}</span>
        </div>
      </div>
      <Badge variant="outline" className="text-[10px] capitalize">{item.status}</Badge>
    </div>
  );
};

export default function Index() {
  const { token, email } = useAuth();
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState("");

  const dashboardQuery = useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api.dashboard(token!),
    enabled: !!token,
  });

  const productsQuery = useQuery({
    queryKey: ["products-home"],
    queryFn: () => api.products(token!),
    enabled: !!token,
  });

  const recipesQuery = useQuery({
    queryKey: ["recipes-home"],
    queryFn: () => api.recipes(token!),
    enabled: !!token,
  });

  const today = new Date();
  const allProductBatches = useMemo(() => {
    return (productsQuery.data?.items ?? []).flatMap((p) =>
      p.batches.map((b) => {
        const days = Math.ceil((new Date(b.expiryDate).getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
        const status = days < 0 ? "expired" : days <= 3 ? "expiring" : "safe";
        return { name: p.name, expiry: b.expiryDate, days, status };
      })
    );
  }, [productsQuery.data, today.toDateString()]);

  const expiringProducts = useMemo(() => {
    return allProductBatches
      .filter((p) => p.status === "expiring" || p.status === "expired")
      .sort((a, b) => a.days - b.days)
      .slice(0, 5)
      .map((p, idx) => ({
        ...p,
        color: p.days <= 0 ? "text-red-500 border-red-200 bg-red-50" : "text-orange-500 border-orange-200 bg-orange-50",
        image: [
          "https://images.unsplash.com/photo-1563636619-e9107da5a165?auto=format&fit=crop&w=300&q=80",
          "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=300&q=80",
          "https://images.unsplash.com/photo-1597362925123-77861d3fbac7?auto=format&fit=crop&w=300&q=80",
        ][idx % 3],
      }));
  }, [allProductBatches]);

  const searchResults = useMemo(() => {
    if (!searchQuery) return [];
    return allProductBatches.filter((item) => item.name.toLowerCase().includes(searchQuery.toLowerCase()));
  }, [allProductBatches, searchQuery]);

  const suggestion = recipesQuery.data?.[0];
  const expiredCount = allProductBatches.filter((p) => p.status === "expired").length;

  return (
    <div className="flex flex-col gap-6 pb-24 animate-in fade-in duration-500">
      <div className="px-6 pt-6 flex flex-col gap-1">
        <h2 className="text-2xl font-bold text-foreground leading-tight">Hello, {email?.split("@")[0] ?? "User"}!</h2>
        <p className="text-sm text-muted-foreground">Ready to save some food today?</p>
      </div>

      <div className="px-6 grid grid-cols-3 gap-3">
        <SummaryCard title="Total Items" count={dashboardQuery.data?.totalProducts ?? 0} color="text-primary" bg="bg-primary/10" />
        <SummaryCard title="Expiring Soon" count={dashboardQuery.data?.expiringSoonCount ?? 0} color="text-orange-600" bg="bg-orange-50" />
        <SummaryCard title="Expired" count={expiredCount} color="text-red-600" bg="bg-red-50" />
      </div>

      <div className="px-6 flex flex-col gap-4">
        <div className="relative group">
          <Search className="absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground group-focus-within:text-primary transition-colors" />
          <Input
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search items, recipes..."
            className="pl-11 h-12 rounded-2xl border-border bg-card shadow-sm focus:ring-primary/20"
          />
        </div>

        {!searchQuery && (
          <div className="grid grid-cols-2 gap-3">
            <Button
              variant="outline"
              onClick={() => navigate("/scan?source=pantry")}
              className="h-14 rounded-2xl bg-card border-border shadow-sm flex items-center justify-center gap-2 hover:bg-muted/50 transition-colors"
            >
              <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center">
                <Plus className="h-4 w-4 text-primary" />
              </div>
              <span className="font-semibold text-foreground">Add Item</span>
            </Button>
            <Button
              variant="outline"
              onClick={() => navigate("/scan")}
              className="h-14 rounded-2xl bg-card border-border shadow-sm flex items-center justify-center gap-2 hover:bg-muted/50 transition-colors"
            >
              <div className="w-8 h-8 rounded-full bg-blue-500/10 flex items-center justify-center">
                <ScanLine className="h-4 w-4 text-blue-500" />
              </div>
              <span className="font-semibold text-foreground">Scan Receipt</span>
            </Button>
          </div>
        )}
      </div>

      {searchQuery ? (
        <div className="px-6 flex flex-col gap-3 animate-in slide-in-from-bottom-4 duration-300">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-bold text-muted-foreground uppercase tracking-wider">Search Results</h2>
            <Link to="/pantry" className="text-xs font-bold text-primary flex items-center">Open Pantry <ArrowRight className="h-3 w-3 ml-1" /></Link>
          </div>
          {searchResults.length > 0 ? (
            <div className="flex flex-col gap-2">
              {searchResults.map((item, idx) => (
                <SearchResultItem key={idx} item={item} />
              ))}
            </div>
          ) : (
            <div className="p-8 text-center bg-muted/20 border border-border rounded-2xl">
              <p className="text-muted-foreground text-sm font-medium">No ingredients found for "{searchQuery}".</p>
              <Button
                variant="outline"
                className="mt-4 rounded-xl"
                onClick={() => navigate("/scan?source=pantry")}
              >
                <Plus className="h-4 w-4 mr-2" /> Add new item
              </Button>
            </div>
          )}
        </div>
      ) : (
        <>
          <div className="px-6">
            <div className="p-5 rounded-[2rem] bg-gradient-to-br from-primary/10 to-primary/5 border border-primary/20 shadow-sm flex flex-col gap-4">
              <div className="flex items-center gap-2 text-primary">
                <Sparkles className="h-5 w-5 fill-primary/20" />
                <span className="text-sm font-bold uppercase tracking-wider">Today's Suggestion</span>
              </div>
              <div className="flex gap-4">
                <div className="w-20 h-20 rounded-2xl bg-card flex items-center justify-center shadow-sm">
                  <ChefHat className="h-10 w-10 text-primary" />
                </div>
                <div className="flex flex-col gap-1 justify-center">
                  <h3 className="font-bold text-foreground">{suggestion?.name ?? "No recipe yet"}</h3>
                  <p className="text-xs text-muted-foreground">
                    {suggestion ? `${Math.round(suggestion.matchPercent)}% ingredient match, score ${suggestion.finalScore.toFixed(1)}` : "Add pantry items to get suggestions"}
                  </p>
                </div>
              </div>
              <Link to="/recipes">
                <div className="w-full bg-primary text-white py-3 rounded-2xl text-center font-bold text-sm shadow-lg shadow-primary/20 flex items-center justify-center gap-2">
                  See Recipe Details <ChevronRight className="h-4 w-4" />
                </div>
              </Link>
            </div>
          </div>

          <div className="flex flex-col gap-4">
            <div className="flex items-center justify-between px-6">
              <h2 className="text-lg font-bold text-foreground">Expiring Soon</h2>
              <Link to="/pantry" className="text-xs font-semibold text-primary flex items-center">
                View All <ChevronRight className="h-4 w-4" />
              </Link>
            </div>
            <div className="flex overflow-x-auto px-6 gap-4 no-scrollbar">
              {expiringProducts.length > 0 ? (
                expiringProducts.map((p, idx) => (
                  <ExpiringProductCard key={idx} name={p.name} expiry={p.expiry} days={p.days} color={p.color} image={p.image} />
                ))
              ) : (
                <div className="w-full p-4 text-center rounded-2xl bg-muted/20 border border-border">
                  <p className="text-sm text-muted-foreground">No products expiring soon.</p>
                </div>
              )}
            </div>
          </div>

          <div className="mx-6 p-4 rounded-2xl bg-card border border-border flex items-center justify-between shadow-sm">
            <div className="flex flex-col">
              <span className="font-bold text-xs text-primary">Storage Tip</span>
              <p className="text-[11px] text-muted-foreground max-w-[200px]">Keep bread dry and sealed; refrigerate dairy immediately after purchase.</p>
            </div>
            <div className="w-10 h-10 bg-primary/5 rounded-xl flex items-center justify-center">
              <Utensils className="h-5 w-5 text-primary" />
            </div>
          </div>
        </>
      )}
    </div>
  );
}
