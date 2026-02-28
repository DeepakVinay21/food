export type RecipeGuide = {
  id: string;
  name: string;
  image: string;
  ingredients: string[];
  steps: string[];
};

export const recipeGuides: RecipeGuide[] = [
  {
    id: "tomato-soup",
    name: "Creamy Tomato Basil Soup",
    image: "https://images.unsplash.com/photo-1547592166-23ac45744acd?auto=format&fit=crop&w=900&q=80",
    ingredients: [
      "Tomato - 500g",
      "Milk - 250ml",
      "Onion - 1 medium",
      "Butter - 1 tbsp",
      "Salt - 2 tsp",
      "Black pepper - 1 tsp",
      "Basil - 10 leaves"
    ],
    steps: [
      "Heat butter in a pan and saute chopped onion for 2 minutes.",
      "Add chopped tomatoes and cook until soft for 8 minutes.",
      "Blend the tomato-onion mix until smooth.",
      "Return to pan, add milk gradually and stir continuously.",
      "Add salt, pepper, and basil leaves.",
      "Simmer 5 minutes and serve hot."
    ]
  },
  {
    id: "veggie-stir-fry",
    name: "Healthy Veggie Stir Fry",
    image: "https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=80",
    ingredients: [
      "Onion - 1 large",
      "Tomato - 2 medium",
      "Mixed vegetables - 400g",
      "Oil - 2 tbsp",
      "Salt - 1.5 tsp",
      "Soy sauce - 1 tbsp"
    ],
    steps: [
      "Heat oil and add onion slices.",
      "Add mixed vegetables and stir fry on high flame for 4 minutes.",
      "Add tomato pieces and cook 2 more minutes.",
      "Add salt and soy sauce, mix well.",
      "Cook for 1 minute and serve."
    ]
  },
  {
    id: "omelette",
    name: "Classic French Omelette",
    image: "https://images.unsplash.com/photo-1525351484163-7529414344d8?auto=format&fit=crop&w=900&q=80",
    ingredients: [
      "Eggs - 3",
      "Milk - 2 tbsp",
      "Butter - 1 tsp",
      "Salt - 0.5 tsp",
      "Pepper - 0.25 tsp"
    ],
    steps: [
      "Beat eggs with milk, salt, and pepper.",
      "Heat butter in a non-stick pan.",
      "Pour egg mixture and cook on medium flame.",
      "Gently fold when almost set.",
      "Cook 1 more minute and serve immediately."
    ]
  }
];

export function pickRecipeGuide(name: string): RecipeGuide {
  const lower = name.toLowerCase();
  if (lower.includes("soup") || lower.includes("tomato")) return recipeGuides[0];
  if (lower.includes("stir") || lower.includes("veggie")) return recipeGuides[1];
  return recipeGuides[2];
}
