import { Link } from 'react-router-dom';
import { EmptyState } from '../components/Ui';

export default function NotFound() {
  return <div className="container-page py-16"><EmptyState icon="🧭" title="Page not found" action={<Link to="/" className="btn-primary">Back to home</Link>} /></div>;
}
