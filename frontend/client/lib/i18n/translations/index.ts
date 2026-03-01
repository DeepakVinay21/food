import { en, type TranslationKeys } from "./en";
import { hi } from "./hi";
import { te } from "./te";

export type Language = "English" | "Hindi" | "Telugu";

export const translations: Record<Language, Record<TranslationKeys, string>> = {
  English: en,
  Hindi: hi,
  Telugu: te,
};

export type { TranslationKeys } from "./en";
