import { createContext, useContext } from "react";
import type { AuthResponse, AuthUser } from "../../types/api";

export type AuthStatus = "loading" | "authenticated" | "anonymous";

export interface AuthContextValue {
  status: AuthStatus;
  user: AuthUser | null;
  login: (userName: string, password: string) => Promise<AuthUser>;
  logout: () => Promise<void>;
  applySession: (session: AuthResponse) => void;
  hasPermission: (permission: string) => boolean;
  hasRole: (role: string) => boolean;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth phải nằm trong AuthProvider");
  }
  return context;
}
