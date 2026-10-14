# ADR-0094 — Inventory presenter construction

**Status:** Accepted (HUD-011, 2026-10-14; Tech lead).

## Context

SPEC-61 assigns InventoryPresenter to Presentation and construction/session rebinding to App. It defines the existing IInventoryCommands interface and fixed InventoryViewModel identities, but not presenter construction signatures.

## Proposal

Add the task-owned public InventoryPresenter(InventoryViewModel), Bind(InventoryModel, Wallet), Unbind() and Dispose(). Bind receives the concrete ECO-003 model and wallet, subscribes to SlotChanged/Reset/Wallet.Changed, and projects into the same 200 existing row models. Unbind releases those sources and clears displayed cargo state. App's composition owner rebinds on SessionReset and disposes the presenter at shutdown. No new global service, model writer, assembly reference or existing binding signature.

Select/Move/Sort/StackAll implement the existing command interface. Move delegates to TryMove; opening or rebinding only reads the inventory. Missing-item OriginalId is preserved in the row identity. WalletText uses invariant thousands separators.

## Consequences

The inventory screen is independently constructible against actual owners while the lead completes normal production registration/navigation. The fixed UI pool survives source rebinds. Screen push/pop and attach/detach own view subscriptions and captured-pointer drag state.
