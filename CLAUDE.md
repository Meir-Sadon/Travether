# Travether — instructions for Claude

Product spec and roadmap: [PLAN.md](PLAN.md). Model/session routing: [PHASE_MODEL_MAP.md](PHASE_MODEL_MAP.md).

## Mandatory model check before starting any plan step

Before writing code, running research or making changes for any step of PLAN.md:

1. **Identify the step.** Find the step ID (e.g. `1.6`) in PHASE_MODEL_MAP.md that matches the request. If the request doesn't clearly map to a step, ask the user which step it is.
2. **Identify your model.** Use the model ID from your system prompt / environment info (in Claude Code cloud sessions, the `get_session` tool reports the serving model). If you can't determine it, say so and ask.
3. **Compare** with the "Model" column for that step.
   - **Match** → say in one line: "Step X.Y — recommended model: <model> ✓" and proceed.
   - **Mismatch** → **stop before doing any work** and ask the user to confirm, e.g.:
     "Step X.Y is mapped to <recommended model>, but this session is running <your model>. Continue anyway, or switch models?" Wait for an explicit answer.
4. **Session check.** If the step is marked 🆕 New session but this conversation already contains substantial work on other steps, mention it and suggest opening a new session (the user may choose to continue).

The "Any — small bug fix / typo" row applies to quick fixes outside the plan steps. A mismatch there only needs a one-line note, not a blocking confirmation.

## Conventions

- After finishing a step, tick its checkbox in PLAN.md and commit.
- Don't put model identifiers in commit messages, code or docs other than PHASE_MODEL_MAP.md / CLAUDE.md.
