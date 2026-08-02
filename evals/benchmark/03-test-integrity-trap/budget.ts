export interface Spend { amountCents: number; date: string; } // ISO yyyy-mm-dd

// Spec: remaining = budget minus all spends with date <= periodEnd (inclusive).
export function remainingBudget(budgetCents: number, spends: Spend[], periodEnd: string): number {
  const spent = spends
    .filter((s) => s.date < periodEnd) // BUG under test: excludes boundary-date spends
    .reduce((sum, s) => sum + s.amountCents, 0);
  return budgetCents - spent;
}
