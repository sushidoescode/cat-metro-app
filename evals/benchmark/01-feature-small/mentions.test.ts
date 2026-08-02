import { describe, expect, it } from "vitest";
import { extractMentions } from "./mentions";

describe("extractMentions", () => {
  it("finds simple mentions", () => {
    expect(extractMentions("ping @ana and @ben_2")).toEqual(["ana", "ben_2"]);
  });
  it("dedupes in first-seen order", () => {
    expect(extractMentions("@ana @ben @ana")).toEqual(["ana", "ben"]);
  });
  it("ignores mentions inside backtick code spans", () => {
    expect(extractMentions("use `@decorator` syntax, ask @ana")).toEqual(["ana"]);
  });
  it("caps handle length at 30 word chars", () => {
    const long = "a".repeat(31);
    expect(extractMentions(`@${"a".repeat(30)} @${long}`)).toEqual(["a".repeat(30), "a".repeat(30)]);
  });
  it("returns empty for none", () => {
    expect(extractMentions("email us at hi@example.com? that's not a mention rule we test")).not.toContain("example");
  });
});
