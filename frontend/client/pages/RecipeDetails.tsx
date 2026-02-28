import { useLocation, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { ArrowLeft } from "lucide-react";
import { RecipeSuggestion, api } from "@/lib/api";
import { useAuth } from "@/components/auth/AuthProvider";
import { useQuery } from "@tanstack/react-query";
import { useParams } from "react-router-dom";

export default function RecipeDetails() {
  const location = useLocation();
  const navigate = useNavigate();
  const { token } = useAuth();
  const { id } = useParams();

  const routeStateRecipe = (location.state as { recipe?: RecipeSuggestion } | undefined)?.recipe;
  const recipesQuery = useQuery({
    queryKey: ["recipes"],
    queryFn: () => api.recipes(token!),
    enabled: !!token,
  });
  const fromQuery = recipesQuery.data?.find((r) => r.recipeId === id);
  const recipe = routeStateRecipe?.recipeId === id ? routeStateRecipe : fromQuery;

  if (!recipe) {
    return (
      <div className="p-6">
        <Button variant="ghost" onClick={() => navigate(-1)}>
          <ArrowLeft className="h-4 w-4 mr-2" />Back
        </Button>
        <p className="mt-4 text-sm text-muted-foreground">Recipe details unavailable. Open recipe from the recipe list.</p>
      </div>
    );
  }

  return (
    <div className="p-6 pb-24 flex flex-col gap-5">
      <Button variant="ghost" className="w-fit" onClick={() => navigate(-1)}>
        <ArrowLeft className="h-4 w-4 mr-2" />Back
      </Button>

      <img src={recipe.imageUrl} alt={recipe.name} className="w-full h-56 object-cover rounded-3xl border" />
      <h2 className="text-2xl font-bold">{recipe.name}</h2>
      <p className="text-sm text-muted-foreground">{recipe.mealType} • {recipe.region}</p>

      <div className="bg-white dark:bg-card rounded-2xl border p-4">
        <h3 className="font-bold mb-2">Ingredients</h3>
        <ul className="list-disc pl-5 space-y-1 text-sm">
          {recipe.ingredients.map((i) => <li key={i}>{i}</li>)}
        </ul>
      </div>

      <div className="bg-white dark:bg-card rounded-2xl border p-4">
        <h3 className="font-bold mb-2">Cooking Guidance</h3>
        <ol className="list-decimal pl-5 space-y-2 text-sm">
          {recipe.steps.map((s) => <li key={s}>{s}</li>)}
        </ol>
      </div>
    </div>
  );
}
