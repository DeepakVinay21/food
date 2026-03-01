import * as React from "react";
import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { ChevronRight, Camera, ClipboardList, ChefHat, Clock, Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";
import { useTranslation } from "@/lib/i18n/LanguageContext";

export default function Onboarding() {
  const [showSplash, setShowSplash] = useState(true);
  const [currentSlide, setCurrentSlide] = useState(0);
  const navigate = useNavigate();
  const { t } = useTranslation();

  const slides = [
    {
      title: t("onboarding.slide1Title"),
      description: t("onboarding.slide1Desc"),
      icon: Camera,
      color: "bg-green-100 text-green-600",
    },
    {
      title: t("onboarding.slide2Title"),
      description: t("onboarding.slide2Desc"),
      icon: ClipboardList,
      color: "bg-blue-100 text-blue-600",
    },
    {
      title: t("onboarding.slide3Title"),
      description: t("onboarding.slide3Desc"),
      icon: ChefHat,
      color: "bg-orange-100 text-orange-600",
    }
  ];

  useEffect(() => {
    const timer = setTimeout(() => setShowSplash(false), 2000);
    return () => clearTimeout(timer);
  }, []);

  const handleNext = () => {
    if (currentSlide < slides.length - 1) {
      setCurrentSlide(prev => prev + 1);
    } else {
      navigate("/");
    }
  };

  if (showSplash) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center bg-gradient-to-b from-primary to-primary/80 text-white p-6">
        <div className="relative mb-6">
          <div className="w-24 h-24 bg-white rounded-3xl flex items-center justify-center shadow-2xl animate-bounce">
            <div className="relative">
              <Clock className="h-12 w-12 text-primary" />
              <ChefHat className="h-6 w-6 text-orange-500 absolute -top-1 -right-1" />
            </div>
          </div>
        </div>
        <h1 className="text-3xl font-bold mb-2">{t("onboarding.appName")}</h1>
        <p className="text-lg opacity-90 mb-8">{t("onboarding.tagline")}</p>
        <Loader2 className="h-8 w-8 animate-spin opacity-50" />
      </div>
    );
  }

  const slide = slides[currentSlide];

  return (
    <div className="flex-1 flex flex-col p-8 bg-background h-full">
      <div className="flex justify-end pt-4">
        <Button variant="ghost" onClick={() => navigate("/")} className="text-muted-foreground font-semibold">
          {t("onboarding.skip")}
        </Button>
      </div>

      <div className="flex-1 flex flex-col items-center justify-center gap-12 text-center animate-in fade-in slide-in-from-bottom-4 duration-500">
        <div className={cn("w-48 h-48 rounded-[3rem] flex items-center justify-center shadow-inner", slide.color)}>
          <slide.icon className="h-24 w-24" />
        </div>
        <div className="flex flex-col gap-4">
          <h2 className="text-3xl font-bold text-foreground leading-tight">{slide.title}</h2>
          <p className="text-muted-foreground text-lg leading-relaxed">{slide.description}</p>
        </div>
      </div>

      <div className="flex flex-col gap-8 pb-12 items-center">
        <div className="flex gap-2">
          {slides.map((_, i) => (
            <div
              key={i}
              className={cn(
                "h-2 rounded-full transition-all duration-300",
                currentSlide === i ? "w-8 bg-primary" : "w-2 bg-muted-foreground/30"
              )}
            />
          ))}
        </div>

        <Button
          onClick={handleNext}
          className="w-full h-14 rounded-2xl text-lg font-bold shadow-lg shadow-primary/20 flex items-center justify-center gap-2"
        >
          {currentSlide === slides.length - 1 ? t("onboarding.getStarted") : t("onboarding.next")}
          <ChevronRight className="h-5 w-5" />
        </Button>
      </div>
    </div>
  );
}
