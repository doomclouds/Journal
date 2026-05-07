import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, test, vi } from "vitest";
import App from "./App";

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("App", () => {
  test("renders phase 1 skeleton status", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        app: "Journal.Api",
        status: "ok",
        version: "0.1.0",
        environment: "Development",
        serverTime: "2026-05-07T20:30:00+08:00"
      })
    });
    vi.stubGlobal("fetch", fetchMock);

    render(<App />);

    expect(screen.getByRole("heading", { name: "Journal" })).toBeInTheDocument();
    expect(screen.getByText("Phase 1 Skeleton")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText("online")).toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledWith("http://localhost:5057/health");
    expect(screen.getByText("Journal.Api")).toBeInTheDocument();
    expect(screen.getByText("0.1.0")).toBeInTheDocument();
  });

  test("renders offline state when health check returns non-ok response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: false,
      status: 503,
      json: async () => ({})
    }));

    render(<App />);

    await waitFor(() => expect(screen.getByText("offline")).toBeInTheDocument());
    expect(screen.getByText("health check failed: 503")).toBeInTheDocument();
  });

  test("renders offline state when health check fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new Error("connection refused")));

    render(<App />);

    await waitFor(() => expect(screen.getByText("offline")).toBeInTheDocument());
    expect(screen.getByText("connection refused")).toBeInTheDocument();
  });

  test("restores mocked fetch between tests", () => {
    expect(fetch).not.toHaveProperty("mock");
  });
});
