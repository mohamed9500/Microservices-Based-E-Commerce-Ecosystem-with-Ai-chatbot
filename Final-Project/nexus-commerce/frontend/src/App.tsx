import { lazy, Suspense } from 'react';
import { Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { ProtectedRoute } from './components/ProtectedRoute';
import { Spinner } from './components/Ui';
import CartPage from './pages/Cart';
import Checkout from './pages/Checkout';
import Home from './pages/Home';
import NotFound from './pages/NotFound';
import { OrderDetailPage, OrdersPage } from './pages/Orders';
import ProductDetail from './pages/ProductDetail';
import Products from './pages/Products';
import { LoginPage, ProfilePage, RegisterPage } from './pages/Auth';
import WishlistPage from './pages/Wishlist';

const AdminLayout = lazy(() => import('./admin/AdminLayout').then((m) => ({ default: m.AdminLayout })));
const Dashboard = lazy(() => import('./admin/Dashboard'));
const ProductsAdmin = lazy(() => import('./admin/ProductsAdmin'));
const CategoriesAdmin = lazy(() => import('./admin/CategoriesAdmin'));
const InventoryAdmin = lazy(() => import('./admin/InventoryAdmin'));
const OrdersAdmin = lazy(() => import('./admin/OrdersAdmin'));
const CustomersAdmin = lazy(() => import('./admin/Others').then((m) => ({ default: m.CustomersAdmin })));
const ReviewsAdmin = lazy(() => import('./admin/Others').then((m) => ({ default: m.ReviewsAdmin })));
const ReportsAdmin = lazy(() => import('./admin/Others').then((m) => ({ default: m.ReportsAdmin })));
const MonitoringAdmin = lazy(() => import('./admin/Others').then((m) => ({ default: m.MonitoringAdmin })));

export default function App() {
  return (
    <Suspense fallback={<Spinner />}>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Home />} />
          <Route path="products" element={<Products />} />
          <Route path="products/:id" element={<ProductDetail />} />
          <Route path="cart" element={<CartPage />} />
          <Route path="wishlist" element={<WishlistPage />} />
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<RegisterPage />} />
          <Route element={<ProtectedRoute />}>
            <Route path="checkout" element={<Checkout />} />
            <Route path="orders" element={<OrdersPage />} />
            <Route path="orders/:id" element={<OrderDetailPage />} />
            <Route path="profile" element={<ProfilePage />} />
          </Route>
          <Route path="admin" element={<ProtectedRoute role="Admin" />}>
            <Route element={<AdminLayout />}>
              <Route index element={<Dashboard />} />
              <Route path="products" element={<ProductsAdmin />} />
              <Route path="categories" element={<CategoriesAdmin />} />
              <Route path="inventory" element={<InventoryAdmin />} />
              <Route path="orders" element={<OrdersAdmin />} />
              <Route path="customers" element={<CustomersAdmin />} />
              <Route path="reviews" element={<ReviewsAdmin />} />
              <Route path="reports" element={<ReportsAdmin />} />
              <Route path="monitoring" element={<MonitoringAdmin />} />
            </Route>
          </Route>
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </Suspense>
  );
}
