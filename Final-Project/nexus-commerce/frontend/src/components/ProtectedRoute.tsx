import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../store/auth';

/** UI-level guard only. The backend enforces authentication and roles on every request. */
export function ProtectedRoute({ role }: { role?: 'Admin' }) {
  const { token, user } = useAuth();
  const location = useLocation();
  if (!token || !user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  if (role && user.role !== role) return <Navigate to="/" replace />;
  return <Outlet />;
}
