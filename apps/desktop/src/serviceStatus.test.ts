import { describe, expect, it } from "vitest";
import { getLocalServiceStatusLabel } from "./serviceStatus";

describe("getLocalServiceStatusLabel", () => {
  it("labels reused backend processes explicitly", () => {
    expect(getLocalServiceStatusLabel("reused")).toBe("复用上次残留进程");
  });

  it("labels failed startup as a connection failure", () => {
    expect(getLocalServiceStatusLabel("failed")).toBe("连接失败");
  });
});
