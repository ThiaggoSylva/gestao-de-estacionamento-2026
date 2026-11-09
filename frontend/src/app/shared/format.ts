const currencyFormatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const dateTimeFormatter = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' });

export function formatCurrency(value: number | null | undefined): string {
  return value == null ? '—' : currencyFormatter.format(value);
}

export function formatDateTime(value: string | null | undefined): string {
  return value ? dateTimeFormatter.format(new Date(value)) : '—';
}

const stateLabels: Record<string, string> = {
  Active: 'Ativo',
  AwaitingWithdrawal: 'Aguardando retirada',
  Searching: 'Em busca',
  Delivered: 'Entregue',
  Awaiting: 'Aguardando',
  Approved: 'Aprovada',
  Divergent: 'Divergente',
  Cash: 'Dinheiro',
  Card: 'Cartão',
  Pix: 'Pix',
  DocumentDivergence: 'Divergência documental',
  OutstandingBalance: 'Saldo pendente',
};

export function formatState(value: string | null | undefined): string {
  return value ? (stateLabels[value] ?? value) : '—';
}
