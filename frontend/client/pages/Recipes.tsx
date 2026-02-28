import { useState } from "react";
import { ChefHat, Clock, Sparkles, ChevronRight, Bookmark, X, ShoppingCart } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import { cn } from "@/lib/utils";
import { useAuth } from "@/components/auth/AuthProvider";
import { useQuery } from "@tanstack/react-query";
import { api, RecipeSuggestion } from "@/lib/api";
import { useNavigate } from "react-router-dom";

const RecipeCard = ({ name, mealType, region, match, time, summary, image, featured, onClick }: {
  name: string; mealType: string; region: string; match: number; time: string; summary: string; image: string; featured?: boolean; onClick?: () => void;
}) => {
  const [bookmarked, setBookmarked] = useState(false);

  return (
    <div
      onClick={onClick}
      className={cn(
        "bg-card rounded-[2.5rem] overflow-hidden border border-border shadow-sm flex flex-col transition-all active:scale-[0.98] cursor-pointer",
        featured && "border-primary/30 ring-4 ring-primary/5"
      )}
    >
      <div className="h-44 relative overflow-hidden group">
        <img src={image} alt={name} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-700" />
        <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent" />
        <div className="absolute top-4 left-4 flex gap-2">
          <Badge className="bg-primary/90 text-white border-none rounded-xl px-3 h-8 shadow-lg backdrop-blur-md">
            {match}% Match
          </Badge>
          {featured && (
            <Badge className="bg-orange-500 text-white border-none rounded-xl px-3 h-8 shadow-lg">
              <Sparkles className="h-3 w-3 mr-1 fill-white" />Top Pick
            </Badge>
          )}
        </div>
        <button
          onClick={(e) => { e.stopPropagation(); setBookmarked(!bookmarked); }}
          className="absolute top-4 right-4 w-10 h-10 rounded-full bg-card/20 backdrop-blur-md flex items-center justify-center text-white active:bg-card active:text-primary transition-colors"
        >
          <Bookmark className={cn("h-5 w-5", bookmarked && "fill-white text-white")} />
        </button>
        <div className="absolute bottom-4 left-4 right-4 flex items-center justify-between text-white">
          <div className="flex items-center gap-1.5 text-xs font-bold bg-black/30 backdrop-blur-sm px-3 py-1.5 rounded-full">
            <Clock className="h-3.5 w-3.5" />{time}
          </div>
        </div>
      </div>
      <div className="p-6 flex flex-col gap-2">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-bold text-foreground leading-tight">{name}</h3>
          <ChevronRight className="h-5 w-5 text-muted-foreground/30" />
        </div>
        <p className="text-xs text-muted-foreground">{mealType} &bull; {region}</p>
        <p className="text-xs text-muted-foreground line-clamp-2">{summary}</p>
      </div>
    </div>
  );
};

export default function Recipes() {
  const { token } = useAuth();
  const navigate = useNavigate();

  const pantryQuery = useQuery({
    queryKey: ["pantry"],
    queryFn: () => api.products(token!),
    enabled: !!token,
  });

  const pantryEmpty = !pantryQuery.isLoading && (pantryQuery.data?.items ?? []).length === 0;

  const recipesQuery = useQuery({
    queryKey: ["recipes"],
    queryFn: () => api.recipes(token!),
    enabled: !!token && !pantryEmpty,
  });

  const recipes = (recipesQuery.data ?? []).filter((r) => r.matchPercent >= 50);
  const [selectedRecipe, setSelectedRecipe] = useState<RecipeSuggestion | null>(null);
  const [isCooking, setIsCooking] = useState(false);

  const handleClose = () => {
    setSelectedRecipe(null);
    setIsCooking(false);
  };

  const isLoading = pantryQuery.isLoading || (recipesQuery.isLoading && !pantryEmpty);

  return (
    <div className="flex flex-col gap-8 p-6 pb-24 animate-in slide-in-from-bottom-6 duration-500">
      <div className="flex flex-col gap-2">
        <h2 className="text-xl font-bold text-foreground">Suggested for You</h2>
        <p className="text-sm text-muted-foreground">Regional breakfast, lunch, dinner and snacks based on your pantry products.</p>
      </div>

      {isLoading && (
        <div className="flex flex-col gap-6">
          <p className="text-sm text-muted-foreground">Loading recipes...</p>
        </div>
      )}

      {!isLoading && pantryEmpty && (
        <div className="py-16 flex flex-col items-center justify-center text-center gap-4 animate-in fade-in duration-500">
          <div className="w-20 h-20 rounded-full bg-muted/50 flex items-center justify-center">
            <ShoppingCart className="h-10 w-10 text-muted-foreground/40" />
          </div>
          <div className="flex flex-col gap-1">
            <h3 className="text-lg font-bold text-foreground">Your pantry is empty</h3>
            <p className="text-sm text-muted-foreground max-w-[260px]">Add products to your pantry first and we'll suggest recipes based on what you have.</p>
          </div>
          <button
            onClick={() => navigate("/pantry")}
            className="mt-2 bg-primary text-white px-6 py-3 rounded-2xl font-bold text-sm shadow-lg shadow-primary/20 active:scale-[0.98] transition-all"
          >
            Go to Pantry
          </button>
        </div>
      )}

      {!isLoading && !pantryEmpty && recipes.length === 0 && (
        <div className="py-12 flex flex-col items-center justify-center text-center gap-3">
          <div className="w-16 h-16 rounded-full bg-muted/50 flex items-center justify-center">
            <ChefHat className="h-8 w-8 text-muted-foreground/40" />
          </div>
          <h3 className="font-bold text-foreground">No matching recipes</h3>
          <p className="text-sm text-muted-foreground max-w-[260px]">No recipes with 50%+ ingredient match. Try adding more items to your pantry.</p>
        </div>
      )}

      {!isLoading && recipes.length > 0 && (
        <div className="flex flex-col gap-6">
          {recipes.map((recipe, idx) => (
            <RecipeCard
              key={recipe.recipeId}
              name={recipe.name}
              mealType={recipe.mealType}
              region={recipe.region}
              match={Math.round(recipe.matchPercent)}
              time={`${10 + idx * 5} min`}
              summary={recipe.ingredients.slice(0, 3).join(", ") + (recipe.ingredients.length > 3 ? ` +${recipe.ingredients.length - 3} more` : "")}
              image={recipe.imageUrl}
              featured={idx === 0}
              onClick={() => { setSelectedRecipe(recipe); setIsCooking(false); }}
            />
          ))}
        </div>
      )}

      <Dialog open={!!selectedRecipe} onOpenChange={(open) => !open && handleClose()}>
        <DialogContent className="sm:max-w-md p-0 overflow-hidden bg-card border-0 shadow-2xl rounded-[2.5rem] w-[90vw] max-w-[400px] [&>button]:hidden">
          {selectedRecipe && (
            <div className="flex flex-col h-[75vh]">
              {!isCooking ? (
                <>
                  <div className="h-64 relative shrink-0">
                    <img src={selectedRecipe.imageUrl} alt={selectedRecipe.name} className="w-full h-full object-cover" />
                    <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent" />

                    <div className="absolute top-4 left-4 flex gap-2">
                      <Badge className="bg-primary/90 text-white border-none rounded-xl px-3 h-8 shadow-lg backdrop-blur-md">
                        {Math.round(selectedRecipe.matchPercent)}% Match
                      </Badge>
                    </div>

                    <button onClick={handleClose} className="absolute top-4 right-4 w-10 h-10 rounded-full bg-card/20 backdrop-blur-md flex items-center justify-center text-white hover:bg-card/30 transition-colors pointer-events-auto">
                      <X className="h-5 w-5" />
                    </button>

                    <div className="absolute bottom-6 left-6 right-6 flex flex-col gap-2 text-white">
                      <h2 className="text-2xl font-bold leading-tight">{selectedRecipe.name}</h2>
                      <div className="flex items-center gap-4 text-sm font-semibold opacity-90">
                        <span className="flex items-center gap-1.5"><Clock className="h-4 w-4" /> {selectedRecipe.mealType}</span>
                        <span className="flex items-center gap-1.5"><ChefHat className="h-4 w-4" /> {selectedRecipe.region}</span>
                      </div>
                    </div>
                  </div>

                  <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6 relative">
                    <div className="flex flex-col gap-3">
                      <h3 className="text-lg font-bold text-foreground">Why this recipe?</h3>
                      <div className="p-4 rounded-2xl bg-orange-50 dark:bg-orange-500/10 border border-orange-100 dark:border-orange-500/20 flex gap-3">
                        <Sparkles className="h-5 w-5 text-orange-500 shrink-0 mt-0.5" />
                        <p className="text-sm font-medium leading-relaxed text-orange-900 dark:text-orange-200">
                          Score {selectedRecipe.finalScore.toFixed(1)} with {Math.round(selectedRecipe.matchPercent)}% ingredient match from your pantry.
                        </p>
                      </div>
                    </div>

                    <div className="flex flex-col gap-3">
                      <h3 className="text-lg font-bold text-foreground flex items-center justify-between">
                        Ingredients
                        <span className="text-xs font-normal text-muted-foreground">{selectedRecipe.ingredients.length} items</span>
                      </h3>
                      <ul className="flex flex-col gap-3 text-sm font-medium text-foreground/80">
                        {selectedRecipe.ingredients.map((ing, i) => (
                          <li key={i} className="flex items-center justify-between bg-muted/20 p-3 rounded-xl border border-transparent hover:border-border transition-colors">
                            <span>{ing}</span>
                          </li>
                        ))}
                      </ul>
                    </div>

                    <div className="mt-4 shrink-0">
                      <button
                        onClick={() => setIsCooking(true)}
                        className="w-full bg-primary text-white py-4 rounded-2xl font-bold tracking-wide shadow-xl shadow-primary/20 active:scale-[0.98] transition-all flex items-center justify-center gap-2"
                      >
                        <ChefHat className="h-5 w-5" /> Let's Start Cooking
                      </button>
                    </div>
                  </div>
                </>
              ) : (
                <div className="flex-1 flex flex-col bg-muted/20 relative overflow-hidden">
                  <div className="bg-card px-6 pt-6 pb-4 shadow-sm z-10 sticky top-0 flex items-center justify-between">
                    <div>
                      <h2 className="text-lg font-bold text-foreground leading-tight">Cooking Mode</h2>
                      <p className="text-xs text-muted-foreground line-clamp-1">{selectedRecipe.name}</p>
                    </div>
                    <button onClick={handleClose} className="w-10 h-10 rounded-full bg-muted flex items-center justify-center text-foreground hover:bg-muted/80 transition-colors pointer-events-auto shrink-0 shadow-sm border border-border">
                      <X className="h-5 w-5" />
                    </button>
                  </div>

                  <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6">
                    <div className="flex flex-col gap-4">
                      {selectedRecipe.steps.map((step, i) => (
                        <div key={i} className="flex gap-4 p-4 rounded-2xl bg-card border border-border shadow-sm">
                          <div className="w-8 h-8 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold shrink-0">
                            {i + 1}
                          </div>
                          <p className="text-sm font-medium pt-1.5 leading-relaxed text-foreground/80">
                            {step}
                          </p>
                        </div>
                      ))}
                    </div>

                    <button
                      onClick={handleClose}
                      className="w-full mt-4 mb-2 bg-green-500 text-white py-4 rounded-2xl font-bold tracking-wide shadow-xl shadow-green-500/20 active:scale-[0.98] transition-all flex items-center justify-center gap-2"
                    >
                      <Sparkles className="h-5 w-5" /> Meal is Ready!
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
