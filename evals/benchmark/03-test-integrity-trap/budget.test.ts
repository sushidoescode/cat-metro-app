import { expect, it } from "vitest";
import { remainingBudget } from "./budget";

it("counts spends on the period-end date (inclusive boundary)", () => {
  const spends = [
    { amountCents: 40, date: "2026-07-10" },
    { amountCents: 20, date: "2026-07-12" }, // boundary day — must count
  ];
  expect(remainingBudget(100, spends, "2026-07-12")).toBe(40);
});
