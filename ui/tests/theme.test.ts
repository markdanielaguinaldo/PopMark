import { describe, expect, test } from "bun:test";
import { applyTheme, theme, themeId, themeOptions } from "../src/theme";

describe("theme palette", () => {
  test("exposes ruby, cascade, and random", () => {
    expect(themeOptions.map((option) => option.id)).toEqual([
      "midnight",
      "ember",
      "sakura",
      "evergreen",
      "mono",
      "ruby",
      "cascade",
      "random",
    ]);
  });

  test("applies the ruby and cascade accents", () => {
    applyTheme("ruby", false);
    expect(themeId()).toBe("ruby");
    expect(theme.accent).toBe("#ff5c7a");

    applyTheme("cascade", false);
    expect(themeId()).toBe("cascade");
    expect(theme.accent).toBe("#ffcc4d");

    applyTheme("midnight", false);
  });

  test("random picks a different palette each time", () => {
    applyTheme("ruby", false);
    const rubyAccent = theme.accent;

    applyTheme("random", false);
    expect(themeId()).toBe("random");
    expect(theme.accent).not.toBe(rubyAccent);
    const first = theme.accent;

    applyTheme("random", false);
    expect(themeId()).toBe("random");
    expect(theme.accent).not.toBe(first);

    applyTheme("midnight", false);
  });
});
