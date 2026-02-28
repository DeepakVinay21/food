import "./global.css";

import { Toaster } from "@/components/ui/toaster";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import Index from "./pages/Index";
import Pantry from "./pages/Pantry";
import Onboarding from "./pages/Onboarding";
import Scan from "./pages/Scan";
import Recipes from "./pages/Recipes";
import Profile from "./pages/Profile";
import NotFound from "./pages/NotFound";
import { MobileLayout } from "./components/layout/MobileLayout";
import Login from "./pages/Login";
import Register from "./pages/Register";
import { AuthProvider } from "./components/auth/AuthProvider";
import { RequireAuth } from "./components/auth/RequireAuth";
import ChangePassword from "./pages/ChangePassword";
import ProfileEdit from "./pages/ProfileEdit";
import VerifyEmail from "./pages/VerifyEmail";

const queryClient = new QueryClient();

const App = () => (
  <QueryClientProvider client={queryClient}>
    <TooltipProvider>
      <Toaster />
      <Sonner />
      <BrowserRouter>
        <AuthProvider>
          <MobileLayout>
            <Routes>
              <Route path="/login" element={<Login />} />
              <Route path="/register" element={<Register />} />
              <Route path="/verify-email" element={<VerifyEmail />} />

              <Route path="/" element={<RequireAuth><Index /></RequireAuth>} />
              <Route path="/pantry" element={<RequireAuth><Pantry /></RequireAuth>} />
              <Route path="/scan" element={<RequireAuth><Scan /></RequireAuth>} />
              <Route path="/recipes" element={<RequireAuth><Recipes /></RequireAuth>} />
              <Route path="/profile" element={<RequireAuth><Profile /></RequireAuth>} />
              <Route path="/profile/change-password" element={<RequireAuth><ChangePassword /></RequireAuth>} />
              <Route path="/profile/edit" element={<RequireAuth><ProfileEdit /></RequireAuth>} />

              <Route path="/onboarding" element={<Onboarding />} />
              <Route path="/splash" element={<Onboarding />} />
              <Route path="*" element={<NotFound />} />
            </Routes>
          </MobileLayout>
        </AuthProvider>
      </BrowserRouter>
    </TooltipProvider>
  </QueryClientProvider>
);

export default App;
