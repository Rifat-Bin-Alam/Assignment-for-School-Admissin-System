export default function StatusBadge({ status }) {
  const normalized = (status || '').toUpperCase();

  let className = 'status-badge ';
  switch (normalized) {
    case 'CONFIRMED':
      className += 'status-confirmed';
      break;
    case 'HELD':
      className += 'status-held';
      break;
    case 'EXPIRED':
      className += 'status-expired';
      break;
    case 'CANCELLED':
      className += 'status-cancelled';
      break;
    default:
      className += 'status-default';
  }

  return <span className={className}>{normalized || 'UNKNOWN'}</span>;
}
