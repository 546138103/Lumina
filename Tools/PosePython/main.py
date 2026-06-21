from body import BodyThread
import global_vars


def main():
    global_vars.KILL_THREADS = False
    body_thread = BodyThread()
    body_thread.start()

    try:
        # Unity writes one line to stdin when it wants the tracker to stop.
        input()
    except (EOFError, KeyboardInterrupt):
        pass
    finally:
        global_vars.KILL_THREADS = True
        body_thread.join(timeout=5.0)


if __name__ == "__main__":
    main()
