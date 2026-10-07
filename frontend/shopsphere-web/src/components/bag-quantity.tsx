"use client";

import { useState } from "react";
import { Icon } from "@/components/icon";

export function BagQuantity({
  quantity,
  productName,
  disabled,
  busy,
  maximum = 99,
  onChange,
}: {
  quantity: number;
  productName: string;
  disabled: boolean;
  busy: boolean;
  maximum?: number;
  onChange: (quantity: number) => void;
}) {
  const [draft, setDraft] = useState<string | null>(null);
  const entered = draft === null || draft.trim() === "" ? quantity : Number(draft);
  const limit = Math.max(1, Math.min(99, maximum));
  const current = Math.max(1, Math.min(limit, entered));

  function commit(next = current) {
    setDraft(null);
    if (!disabled && next !== quantity) onChange(next);
  }

  return (
    <div className="bag-quantity-field">
      <span className="quantity-caption">Quantity</span>
      <span className="sr-only" role="status">{busy ? "Updating quantity." : ""}</span>
      <div
        className="bag-quantity-picker"
        role="group"
        aria-label={`Quantity for ${productName}`}
        aria-busy={busy}
        onBlur={(event) => {
          // Moving between input and buttons must not send two competing updates.
          if (!event.currentTarget.contains(event.relatedTarget) && draft !== null)
            commit();
        }}
      >
        <button
          type="button"
          aria-label={`Decrease quantity for ${productName}`}
          aria-disabled={disabled || entered <= 1}
          data-limit={entered <= 1 || maximum <= 0 || undefined}
          onClick={() => {
            if (entered > 1) commit(Math.max(1, Math.min(limit, entered - 1)));
          }}
        >
          <Icon name="minus" size={16} />
        </button>
        <input
          type="text"
          inputMode="numeric"
          pattern="[0-9]*"
          maxLength={2}
          aria-label={`Quantity for ${productName}`}
          value={draft ?? quantity}
          readOnly={disabled}
          aria-disabled={disabled}
          onChange={(event) => {
            if (/^\d{0,2}$/.test(event.target.value)) setDraft(event.target.value);
          }}
          onFocus={(event) => event.currentTarget.select()}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              commit();
            } else if (event.key === "Escape") {
              event.preventDefault();
              setDraft(null);
            }
          }}
        />
        <button
          type="button"
          aria-label={`Increase quantity for ${productName}`}
          aria-disabled={disabled || current >= limit}
          data-limit={current >= limit || maximum <= 0 || undefined}
          onClick={() => {
            if (current < limit) commit(current + 1);
          }}
        >
          <Icon name="plus" size={16} />
        </button>
      </div>
    </div>
  );
}
