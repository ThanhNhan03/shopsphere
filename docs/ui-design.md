# ShopSphere interface design

Direction: a considered technology storefront with an editorial layout, warm ivory surfaces, charcoal actions, burnt orange accents, generous product space and self-hosted Manrope typography.

Typography uses one font family across headings, copy and controls. All words within a campaign slogan share the same weight, size, line height and letter spacing; highlighted words change only color. Do not introduce a serif or italic font for individual slogan words. Use weight and size to distinguish semantic roles, and reuse the same style for equivalent roles across pages.

The requested local UI/UX Pro Max skill was read from `E:/ui-ux-pro-max-skill/.claude/skills/ui-ux-pro-max/SKILL.md`. Its design-system searches returned a feature-rich showcase pattern, ecommerce typography and orange accents. The initial Apple Liquid Glass suggestion did not fit this web store; a narrower ecommerce query informed the final synthesis. Accessibility, touch targets, visible focus, reduced motion, content reflow and stack-specific image guidance were applied to the existing Next.js application.

## Tokens

| Purpose | Value |
| --- | --- |
| Canvas | `#FBFBF7` |
| Primary text / action | `#252723` |
| Secondary text | `#696A63` |
| Accent text | `#B34624` |
| Accent decoration | `#F17A4E` |
| Surface | `#F0F1E9` |
| Success | `#286046` |
| Error | `#A62828` |
| Container | 1376px including responsive gutters |
| Primary action target | 52px minimum height |
| Compact controls | 44px touch target |

## Flows

- Home: featured product, actual brand names, name/brand search, price sorting, available products by default, an explicit Include out of stock checkbox, real empty results and an audio collection link. Cards use a page-level live stock batch, clear availability text and disabled unavailable/unknown purchase actions. Existing bag capacity is reflected with a Review your bag link.
- Product: product image, price, live stock availability, quantity controls bounded by available stock, add-to-bag feedback and checkout information.
- Authentication: Google sign-in, account state, different-account action, signed-out prompts and cancellation feedback.
- Bag: Unit price, Quantity and Item total appear in that order, with server-confirmed totals. Minus/input/plus controls support direct entry, 1–99 bounds capped by live availability, keyboard commit/cancel and a fixed `Quantity` caption during updates. Buttons retain focus/appearance during requests and use guarded `aria-disabled` states; the input becomes read-only. A separate screen-reader announcement identifies updates. Returned server baskets update the existing view. Low/out-of-stock messages and checkout eligibility expose availability changes. On narrow screens unit price sits above the quantity/line-total pair below product information.
- Checkout: visible progress, account-prefilled details, inline blur/submit validation and focus on the first invalid field.
- Order/payment: progress, status, order details and existing Demo/Stripe actions.
- Shared states: loading, API errors, empty bag/search and not-found/payment-link recovery.

No reviews, discount claims or delivery promises were invented. Stock counts come from the Inventory API. Authentication/payment contracts are retained. Fonts are served locally; their OFL license is in `frontend/shopsphere-web/public/fonts/OFL.txt`.

Product imagery uses reusable device illustrations from `public/products/`: laptop, headphones, mouse, monitor, keyboard and SSD. `src/lib/device-icons.ts` is the shared registry. Multiple products of the same device type reuse the same icon; no individual model photograph is required. Catalog/basket `imageUrl` identifies the device icon (for example `/products/keyboard.svg`). Known categories also provide a fallback; unknown device types show a generic box icon until a type is registered. Home and sign-in banners reuse this registry. The typography consistency correction remains in place.
