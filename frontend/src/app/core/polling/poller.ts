export class Poller {
  private timer: ReturnType<typeof setInterval> | null = null;
  private controller: AbortController | null = null;
  private started = false;

  constructor(
    private readonly task: (signal: AbortSignal) => Promise<void>,
    private readonly intervalMs = 5000,
  ) {}

  start(): void {
    if (this.started) {
      return;
    }

    this.started = true;
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    this.schedule();
  }

  stop(): void {
    this.started = false;
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    this.clearTimer();
    this.cancel();
  }

  private readonly onVisibilityChange = (): void => {
    if (document.hidden) {
      this.clearTimer();
      this.cancel();
      return;
    }

    this.schedule();
    void this.run();
  };

  private schedule(): void {
    this.clearTimer();
    this.timer = setInterval(() => void this.run(), this.intervalMs);
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  private cancel(): void {
    this.controller?.abort();
    this.controller = null;
  }

  private async run(): Promise<void> {
    if (!this.started || document.hidden || this.controller !== null) {
      return;
    }

    const controller = new AbortController();
    this.controller = controller;
    try {
      await this.task(controller.signal);
    } catch {
      // Background refresh failures keep the last known state; manual actions surface errors.
    } finally {
      if (this.controller === controller) {
        this.controller = null;
      }
    }
  }
}
