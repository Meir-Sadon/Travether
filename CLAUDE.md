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

## Branch naming

Every new branch gets a short, readable name that says what it contains: `<type>/<step-id>-<short-description>`, lowercase, words joined with `-`.

| Type | Use for | Example |
|------|---------|---------|
| `feature/` | New functionality (a PLAN.md step) | `feature/1.3-vacation-cards` |
| `fix/` | Bug fixes | `fix/join-request-double-submit` |
| `docs/` | Plans, research, README, guides | `docs/starting-destinations-research` |
| `design/` | Prototype and design changes | `design/chat-bubbles` |
| `chore/` | Setup, config, dependencies, CI | `chore/0.1-repo-setup` |

- Include the PHASE_MODEL_MAP.md step ID when the work belongs to a plan step; leave it out otherwise.
- Never use random or auto-generated names (e.g. `claude/sweet-wozniak-0glfej`). If the session was started on such a branch, create a properly named branch from it before the first commit and work there, and tell the user the new name.
- One branch (and one PR) per step or topic.
