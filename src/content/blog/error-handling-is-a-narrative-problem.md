---
title: "Error handling is a narrative problem"
date: 2026-05-28
category: "Engineering"
excerpt: "Every error message is a moment where your software tells the user a story about what just happened. Most of our stories are terrible. Here's how I structure errors like a saga — context, conflict, and a path home."
readTime: "9 min"
---

Every error is a moment where your software has to tell a story: *something went wrong, here is what it was, here is what you can do about it.* Most software tells this story the way a drunk skald tells the fall of kings — out of order, missing the important parts, and somehow blaming the audience.

Consider the classic:

```ts
// What the user sees:
"Error: operation failed (code 500)"

// What actually happened:
"The settlement file for 2026-05-27 hasn't arrived from
 the bank yet, so we can't reconcile your payouts."
```

The first version is a shrug. The second is a story — it has a subject, a cause, and an implied resolution (*wait, or contact us*). Nobody files a support ticket about a story they understood.

## The three-act error

Stolen shamelessly from every saga in the canon, a good error has three parts:

**Context** — what were we trying to do? Not internally; narratively. "Saving your draft," not `POST /v2/drafts`.

**Conflict** — what got in the way, in terms the reader can act on? Name the actor. "The bank's file is late" beats "upstream dependency timeout."

**A path home** — what now? Retry, wait, contact, undo. Odysseus spent ten years getting home; your user should get a button.

In code, I encode the three acts as a type, so an error can't ship without its story:

```ts
type StoryError = {
  context: string;    // "Reconciling May payouts"
  conflict: string;   // "Settlement file hasn't arrived"
  pathHome: Action[]; // [{ label: "Retry at 14:00" }]
  cause?: unknown;    // the raw saga, for the logs
};

function tell(e: StoryError): string {
  return `${e.context}: ${e.conflict}.`;
}
```

> An error message is the only part of your interface that users read word by word. It's the worst possible place to stop trying.

## Logs are for the gods, messages are for mortals

The raw cause — stack traces, status codes, the screaming of distant services — belongs in the logs, where engineers (the gods of this particular cosmology) can read it. The user-facing message is a translation layer, and like all translation it is an act of care. Keep both. Never show one to the other's audience.

Since adopting this shape at work, our most-common support ticket ("what does this error mean?") dropped by roughly half. Turns out users don't hate errors. They hate riddles.
