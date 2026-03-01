import React, { createContext, useContext, useState, useCallback, useMemo } from "react";
import { translations, type Language, type TranslationKeys } from "./translations";
import { en } from "./translations/en";

interface LanguageContextValue {
  language: Language;
  setLanguage: (lang: Language) => void;
  t: (key: TranslationKeys, vars?: Record<string, string | number>) => string;
}

const LanguageContext = createContext<LanguageContextValue | null>(null);

function getStoredLanguage(): Language {
  try {
    const prefs = localStorage.getItem("foodtrack_preferences");
    if (prefs) {
      const parsed = JSON.parse(prefs);
      if (parsed.language && parsed.language in translations) {
        return parsed.language as Language;
      }
    }
  } catch {
    // ignore
  }
  return "English";
}

function persistLanguage(lang: Language) {
  try {
    const prefs = localStorage.getItem("foodtrack_preferences");
    const parsed = prefs ? JSON.parse(prefs) : {};
    parsed.language = lang;
    localStorage.setItem("foodtrack_preferences", JSON.stringify(parsed));
  } catch {
    // ignore
  }
}

export function LanguageProvider({ children }: { children: React.ReactNode }) {
  const [language, setLanguageState] = useState<Language>(getStoredLanguage);

  const setLanguage = useCallback((lang: Language) => {
    setLanguageState(lang);
    persistLanguage(lang);
  }, []);

  const t = useCallback(
    (key: TranslationKeys, vars?: Record<string, string | number>): string => {
      let text: string = translations[language]?.[key] ?? en[key] ?? key;
      if (vars) {
        for (const [k, v] of Object.entries(vars)) {
          text = text.replace(new RegExp(`\\{${k}\\}`, "g"), String(v));
        }
      }
      return text;
    },
    [language]
  );

  const value = useMemo(() => ({ language, setLanguage, t }), [language, setLanguage, t]);

  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useTranslation() {
  const ctx = useContext(LanguageContext);
  if (!ctx) {
    throw new Error("useTranslation must be used within a LanguageProvider");
  }
  return ctx;
}
