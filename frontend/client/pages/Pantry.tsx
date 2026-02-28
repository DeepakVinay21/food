import { Search, Filter, Edit2, Trash2, Plus, Clock, ChevronDown, Check } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from "@/components/ui/accordion";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger, DropdownMenuLabel } from "@/components/ui/dropdown-menu";
import { cn } from "@/lib/utils";
import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, Product } from "@/lib/api";
import { useAuth } from "@/components/auth/AuthProvider";
import { useNavigate } from "react-router-dom";

// ---------- Types ----------

type StatusType = "expired" | "expiring" | "safe";

type EnrichedBatch = {
  batchId: string;
  expiryDate: string;
  quantity: number;
  days: number;
  status: StatusType;
};

type ProductGroup = {
  productId: string;
  name: string;
  categoryName: string;
  image: string;
  enrichedBatches: EnrichedBatch[];
  earliestExpiry: string;
  worstStatus: StatusType;
  totalQuantity: number;
  earliestDays: number;
};

// ---------- Helpers ----------

const imageByName = (name: string) => {
  const n = name.toLowerCase();
  if (n.includes("milk")) return "https://images.unsplash.com/photo-1563636619-e9107da5a165?auto=format&fit=crop&w=300&q=80";
  if (n.includes("bread")) return "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=300&q=80";
  if (n.includes("egg")) return "https://images.unsplash.com/photo-1506976785307-8732e854ad03?auto=format&fit=crop&w=300&q=80";
  if (n.includes("tomato")) return "https://images.unsplash.com/photo-1597362925123-77861d3fbac7?auto=format&fit=crop&w=300&q=80";
  if (n.includes("onion")) return "https://images.unsplash.com/photo-1508747703725-7197771375a0?auto=format&fit=crop&w=300&q=80";
  return "https://images.unsplash.com/photo-1586201375761-83865001e31c?auto=format&fit=crop&w=300&q=80";
};

const getStatusColor = (s: string) => {
  switch (s) {
    case "expired": return "bg-red-500";
    case "expiring": return "bg-orange-500";
    default: return "bg-primary";
  }
};

const statusLabel = (status: StatusType, days: number) => {
  if (status === "expired") return "Expired";
  if (status === "expiring") return days === 0 ? "Today" : `${days}d left`;
  return "Safe";
};

const statusTextColor = (status: StatusType) => {
  if (status === "expired") return "text-red-500";
  if (status === "expiring") return "text-orange-500";
  return "text-primary";
};

const progressWidth = (days: number) => `${Math.max(15, 100 - Math.max(days, 0) * 10)}%`;

function groupProducts(products: Product[]): ProductGroup[] {
  const today = new Date();
  return products
    .map((product) => {
      const enrichedBatches: EnrichedBatch[] = product.batches
        .map((b) => {
          const days = Math.ceil((new Date(b.expiryDate).getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
          const status: StatusType = days < 0 ? "expired" : days <= 3 ? "expiring" : "safe";
          return { batchId: b.batchId, expiryDate: b.expiryDate, quantity: b.quantity, days, status };
        })
        .sort((a, b) => new Date(a.expiryDate).getTime() - new Date(b.expiryDate).getTime());

      const earliest = enrichedBatches[0];

      const worstStatus: StatusType = enrichedBatches.some((b) => b.status === "expired")
        ? "expired"
        : enrichedBatches.some((b) => b.status === "expiring")
          ? "expiring"
          : "safe";

      return {
        productId: product.productId,
        name: product.name,
        categoryName: product.categoryName,
        image: imageByName(product.name),
        enrichedBatches,
        earliestExpiry: earliest?.expiryDate ?? "",
        worstStatus,
        totalQuantity: enrichedBatches.reduce((sum, b) => sum + b.quantity, 0),
        earliestDays: earliest?.days ?? 0,
      };
    })
    .filter((g) => g.enrichedBatches.length > 0)
    .sort((a, b) => new Date(a.earliestExpiry).getTime() - new Date(b.earliestExpiry).getTime());
}

// ---------- Components ----------

const SingleBatchCard = ({ group, onConsume }: { group: ProductGroup; onConsume: (batchId: string) => void }) => {
  const batch = group.enrichedBatches[0];
  return (
    <div className="bg-card rounded-[2rem] p-4 border border-border shadow-sm flex items-center gap-4 group animate-in slide-in-from-left duration-300">
      <div className="w-20 h-20 rounded-3xl bg-primary/10 overflow-hidden flex-shrink-0 shadow-inner flex items-center justify-center">
        <img src={group.image} alt={group.name} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-500" />
      </div>
      <div className="flex-1 flex flex-col gap-1">
        <div className="flex items-center justify-between">
          <h3 className="font-bold text-foreground text-base tracking-tight">{group.name} (x{batch.quantity})</h3>
          <div className="flex gap-1">
            <button className="w-8 h-8 flex items-center justify-center text-muted-foreground hover:text-primary transition-colors active:scale-90" onClick={() => onConsume(batch.batchId)} title="Use 1 quantity">
              <Edit2 className="h-3.5 w-3.5" />
            </button>
            <button className="w-8 h-8 flex items-center justify-center text-muted-foreground hover:text-destructive transition-colors active:scale-90" title="Not implemented">
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>
        <p className="text-[11px] text-muted-foreground flex items-center gap-1 font-medium">
          <Clock className="h-3 w-3" />
          Expires: {batch.expiryDate} &bull; {group.categoryName}
        </p>
        <div className="mt-2 flex items-center gap-3">
          <div className="h-1.5 flex-1 rounded-full bg-muted overflow-hidden">
            <div className={cn("h-full rounded-full transition-all duration-1000", getStatusColor(batch.status))} style={{ width: progressWidth(batch.days) }} />
          </div>
          <span className={cn("text-[10px] font-bold uppercase tracking-wider", statusTextColor(batch.status))}>
            {statusLabel(batch.status, batch.days)}
          </span>
        </div>
      </div>
    </div>
  );
};

const BatchSubCard = ({ batch, onConsume }: { batch: EnrichedBatch; onConsume: (batchId: string) => void }) => (
  <div className="bg-muted/50 rounded-2xl p-3 border border-border/50 flex items-center gap-3">
    <div className="flex-1 flex flex-col gap-1">
      <div className="flex items-center justify-between">
        <p className="text-xs text-muted-foreground flex items-center gap-1 font-medium">
          <Clock className="h-3 w-3" />
          Exp: {batch.expiryDate} &nbsp;x{batch.quantity}
        </p>
        <div className="flex gap-1">
          <button className="w-7 h-7 flex items-center justify-center text-muted-foreground hover:text-primary transition-colors active:scale-90" onClick={() => onConsume(batch.batchId)} title="Use 1 quantity">
            <Edit2 className="h-3 w-3" />
          </button>
          <button className="w-7 h-7 flex items-center justify-center text-muted-foreground hover:text-destructive transition-colors active:scale-90" title="Not implemented">
            <Trash2 className="h-3 w-3" />
          </button>
        </div>
      </div>
      <div className="flex items-center gap-3">
        <div className="h-1.5 flex-1 rounded-full bg-background overflow-hidden">
          <div className={cn("h-full rounded-full transition-all duration-1000", getStatusColor(batch.status))} style={{ width: progressWidth(batch.days) }} />
        </div>
        <span className={cn("text-[10px] font-bold uppercase tracking-wider", statusTextColor(batch.status))}>
          {statusLabel(batch.status, batch.days)}
        </span>
      </div>
    </div>
  </div>
);

const MultiBatchCard = ({ group, onConsume }: { group: ProductGroup; onConsume: (batchId: string) => void }) => (
  <Accordion type="single" collapsible className="animate-in slide-in-from-left duration-300">
    <AccordionItem value={group.productId} className="border-none">
      <div className="bg-card rounded-[2rem] border border-border shadow-sm overflow-hidden">
        <AccordionTrigger className="group hover:no-underline p-0 [&>svg]:hidden">
          <div className="p-4 flex items-center gap-4 w-full">
            <div className="w-20 h-20 rounded-3xl bg-primary/10 overflow-hidden flex-shrink-0 shadow-inner">
              <img src={group.image} alt={group.name} className="w-full h-full object-cover" />
            </div>
            <div className="flex-1 flex flex-col gap-1 text-left">
              <div className="flex items-center justify-between">
                <h3 className="font-bold text-foreground text-base tracking-tight">
                  {group.name} (x{group.totalQuantity} total)
                </h3>
                <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground transition-transform duration-200 group-data-[state=open]:rotate-180" />
              </div>
              <p className="text-[11px] text-muted-foreground flex items-center gap-1 font-medium">
                <Clock className="h-3 w-3" />
                Expires: {group.earliestExpiry} &bull; {group.categoryName}
              </p>
              <div className="mt-2 flex items-center gap-3">
                <div className="h-1.5 flex-1 rounded-full bg-muted overflow-hidden">
                  <div className={cn("h-full rounded-full transition-all duration-1000", getStatusColor(group.worstStatus))} style={{ width: progressWidth(group.earliestDays) }} />
                </div>
                <span className={cn("text-[10px] font-bold uppercase tracking-wider", statusTextColor(group.worstStatus))}>
                  {statusLabel(group.worstStatus, group.earliestDays)}
                </span>
              </div>
            </div>
          </div>
        </AccordionTrigger>
        <AccordionContent className="pb-0 pt-0">
          <div className="flex flex-col gap-2 px-4 pb-4">
            {group.enrichedBatches.map((batch) => (
              <BatchSubCard key={batch.batchId} batch={batch} onConsume={onConsume} />
            ))}
          </div>
        </AccordionContent>
      </div>
    </AccordionItem>
  </Accordion>
);

const ProductGroupCard = ({ group, onConsume }: { group: ProductGroup; onConsume: (batchId: string) => void }) => {
  if (group.enrichedBatches.length === 1) {
    return <SingleBatchCard group={group} onConsume={onConsume} />;
  }
  return <MultiBatchCard group={group} onConsume={onConsume} />;
};

// ---------- Main Page ----------

export default function Pantry() {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "expiring" | "expired" | "safe">("all");
  const [selectedCategory, setSelectedCategory] = useState<string>("All Categories");
  const { token } = useAuth();
  const qc = useQueryClient();
  const navigate = useNavigate();

  const pantryQuery = useQuery({
    queryKey: ["pantry"],
    queryFn: () => api.products(token!),
    enabled: !!token,
  });

  const categories = useMemo(() => {
    const cats = (pantryQuery.data?.items ?? []).map((p) => p.categoryName).filter(Boolean);
    return ["All Categories", ...Array.from(new Set(cats))];
  }, [pantryQuery.data]);

  const consume = useMutation({
    mutationFn: (batchId: string) => api.consumeBatch(token!, batchId, 1),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["pantry"] });
      qc.invalidateQueries({ queryKey: ["products-home"] });
      qc.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });

  const items = useMemo(() => {
    const groups = groupProducts(pantryQuery.data?.items ?? []);
    return groups.filter((g) => {
      const matchSearch = g.name.toLowerCase().includes(search.toLowerCase());
      const matchStatus = statusFilter === "all" || g.enrichedBatches.some((b) => b.status === statusFilter);
      const matchCategory = selectedCategory === "All Categories" || g.categoryName === selectedCategory;
      return matchSearch && matchStatus && matchCategory;
    });
  }, [pantryQuery.data, search, statusFilter, selectedCategory]);

  const filters: { label: string; value: "all" | "expiring" | "expired" | "safe" }[] = [
    { label: "All", value: "all" },
    { label: "Expiring", value: "expiring" },
    { label: "Expired", value: "expired" },
    { label: "Safe", value: "safe" },
  ];

  return (
    <div className="flex flex-col gap-6 p-6 pb-40 h-full animate-in fade-in duration-500">
      <div className="sticky top-0 z-20 bg-background/90 backdrop-blur-sm pt-1 pb-2">
        <div className="flex gap-2">
          <div className="relative flex-1 group">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground group-focus-within:text-primary transition-colors" />
            <Input
              placeholder={selectedCategory === "All Categories" ? "Search your pantry..." : `Search ${selectedCategory}...`}
              className="pl-11 h-12 rounded-2xl border-border bg-card shadow-sm focus:ring-primary/20"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" className={cn(
                "h-12 w-12 rounded-2xl p-0 border-border bg-card shadow-sm active:scale-95 transition-all text-muted-foreground hover:bg-muted/50 focus-visible:ring-primary/20 focus-visible:outline-none focus-visible:ring-2",
                selectedCategory !== "All Categories" && "bg-primary/10 border-primary/20 text-primary"
              )}>
                <Filter className={cn("h-5 w-5", selectedCategory !== "All Categories" && "fill-primary/20")} />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56 rounded-2xl border-border shadow-xl p-2 bg-card/80 backdrop-blur-xl">
              <DropdownMenuLabel className="font-bold text-xs uppercase tracking-wider text-muted-foreground mb-1 ml-2">Filter by Category</DropdownMenuLabel>
              {categories.map((category) => (
                <DropdownMenuItem
                  key={category}
                  onClick={() => setSelectedCategory(category)}
                  className="rounded-xl cursor-pointer py-2.5 px-3 font-medium text-sm flex items-center justify-between group hover:bg-muted focus:bg-muted/50"
                >
                  {category}
                  {selectedCategory === category && <Check className="h-4 w-4 text-primary animate-in zoom-in" />}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        <div className="flex gap-2 overflow-x-auto no-scrollbar pb-1 mt-3">
          {filters.map((filter) => (
            <Badge
              key={filter.value}
              onClick={() => setStatusFilter(filter.value)}
              variant={statusFilter === filter.value ? "default" : "outline"}
              className={cn(
                "rounded-xl px-5 h-9 cursor-pointer transition-colors active:scale-95",
                statusFilter === filter.value ? "font-bold shadow-md" : "bg-card border-border text-muted-foreground font-semibold hover:bg-muted/50"
              )}
            >
              {filter.label}
            </Badge>
          ))}
        </div>
      </div>

      <div className="flex flex-col gap-4 mt-2">
        {pantryQuery.isLoading && <p className="text-sm text-muted-foreground">Loading pantry...</p>}
        {!pantryQuery.isLoading && items.length === 0 && (
          <div className="py-12 flex flex-col items-center justify-center text-center gap-2">
            <div className="w-16 h-16 rounded-full bg-muted/50 flex items-center justify-center mb-2">
              <Search className="h-8 w-8 text-muted-foreground/50" />
            </div>
            <h3 className="font-bold text-foreground">No items found</h3>
            <p className="text-sm text-muted-foreground">Try adjusting your filters or search term.</p>
          </div>
        )}
        {items.map((group) => (
          <ProductGroupCard key={group.productId} group={group} onConsume={(id) => consume.mutate(id)} />
        ))}
      </div>

      <Button onClick={() => navigate("/scan?source=pantry")} className="fixed bottom-24 right-6 w-14 h-14 rounded-2xl shadow-xl shadow-primary/30 z-50 animate-in slide-in-from-bottom-10">
        <Plus className="h-6 w-6" />
      </Button>
    </div>
  );
}
