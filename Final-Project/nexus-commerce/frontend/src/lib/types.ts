export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export interface Category { id: string; name: string; slug: string; description?: string | null; imageUrl?: string | null; sortOrder: number }
export interface Product {
  id: string; sku: string; name: string; slug: string; description: string; price: number; currency: string;
  categoryId: string; categoryName: string; categorySlug: string; brand: string; imageUrl: string;
  gallery: string[]; specs: Record<string, string>; isActive: boolean; isFeatured: boolean; createdAtUtc: string;
}
export type StockStatus = 'in_stock' | 'low_stock' | 'out_of_stock' | 'unknown';
export interface StockInfo { productId: string; available: number; status: StockStatus }
export interface RatingSummary { productId: string; average: number; count: number }
export interface Review { id: string; productId: string; userName: string; rating: number; title: string; body: string; verifiedPurchase: boolean; createdAtUtc: string; isVisible?: boolean }
export interface User { id: string; email: string; fullName: string; role: 'Admin' | 'Customer'; phone?: string | null; addressLine?: string | null; city?: string | null; postalCode?: string | null; country?: string | null; isActive: boolean; createdAtUtc: string; lastLoginAtUtc?: string | null }
export interface AuthResponse { accessToken: string; expiresAtUtc: string; user: User }
export interface CartItem { productId: string; sku: string; name: string; imageUrl: string; unitPrice: number; quantity: number; lineTotal: number }
export interface Cart { items: CartItem[]; itemCount: number; subtotal: number; currency: string; warnings: string[] }
export type OrderStatus = 'Pending' | 'AwaitingPayment' | 'Confirmed' | 'Shipped' | 'Delivered' | 'Cancelled';
export interface OrderItem { productId: string; sku: string; name: string; imageUrl: string; unitPrice: number; quantity: number; lineTotal: number }
export interface Order {
  id: string; status: OrderStatus; subtotal: number; shippingFee: number; total: number; currency: string; paymentMethod: string; paymentIsMock: boolean;
  shipName: string; shipLine1: string; shipCity: string; shipPostalCode: string; shipCountry: string; shipPhone: string;
  failureReason?: string | null; createdAtUtc: string; updatedAtUtc: string; items: OrderItem[];
  userId?: string; userEmail?: string; customerName?: string;
}
export interface OrderStats { totalOrders: number; revenueOrders: number; revenue: number; averageOrderValue: number; byStatus: Record<string, number>; daily: { date: string; orders: number; revenue: number }[]; topProducts: { productId: string; name: string; quantity: number; revenue: number }[] }
export interface AdminStock { productId: string; onHand: number; reserved: number; available: number; updatedAtUtc: string }
export interface Payment { id: string; orderId: string; amount: number; currency: string; provider: string; isMock: boolean; status: string; failureReason?: string; createdAtUtc: string; notice: string }
export interface ProductCardData { id: string; name: string; price: number; currency: string; imageUrl: string; category: string; brand: string; availability: StockStatus; rating?: number | null }
export interface ChatResponse { conversationId: string | null; reply: string; mode: 'llm' | 'fallback'; provider?: string | null; degraded: boolean; notice?: string | null; products: ProductCardData[] }
export interface AssistantStatus { mode: 'llm' | 'fallback'; provider?: string | null; message: string }
export interface ServiceHealth { name: string; healthy: boolean; status: number; latencyMs: number; error?: string | null }
