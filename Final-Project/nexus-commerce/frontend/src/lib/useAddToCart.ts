import { useNavigate } from 'react-router-dom';
import { ApiError } from './api';
import { useT } from './i18n';
import { useAuth } from '../store/auth';
import { useCart } from '../store/cart';
import { toast } from '../store/toast';

/** Adds a product to the server-side cart; guests are sent to sign in (the cart is stored per customer). */
export function useAddToCart() {
  const token = useAuth((s) => s.token);
  const add = useCart((s) => s.add);
  const navigate = useNavigate();
  const { t } = useT();
  return async (productId: string, name?: string, quantity = 1) => {
    if (!token) {
      toast.info(t('cart.loginRequired'));
      navigate('/login', { state: { from: '/products' } });
      return;
    }
    try {
      await add(productId, quantity);
      toast.success(name ? `${name} added to cart` : 'Added to cart');
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : 'Could not add to cart.');
    }
  };
}
