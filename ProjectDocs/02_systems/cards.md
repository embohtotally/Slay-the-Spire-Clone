# Cards and Hand Mechanics

**Purpose:**
Manages the player's deck, active hand, discard pile, and the flow of playing cards via user input (drag-and-drop targeting or manual spline-based drop area).

**Key Classes:**
- `CardSystem`: System tracking card state collections and executing draw/play rules.
- `CardView`: MonoBehaviour visualizing the card and handling pointer drag inputs.
- `HandView`: Positions card views smoothly along a Bezier spline using DOTween.
- `ManualTargetingSystem`: Visualizes raycast targeting lines for single-target cards.

**Public API (CardSystem):**
- `Setup(List<CardData> deck)`: Initializes the draw pile from run data.
- **Action Performers registered:**
  - `DrawCardsGA`: Pulls from draw pile; auto-shuffles discard if draw is empty.
  - `DiscardAllCardsGA`: Empties the hand.
  - `PlayCardGA`: Removes card from hand, deducts mana, queues card effects.
- *(Internal)* `EnemyTurnGA` Subscriptions: Discards hand on PRE, draws 5 on POST.

**Data Flow:**
1. Player clicks `CardView` -> Card leaves hand spline formatting.
2. If manual targeting: User drags arrow over enemy -> `PlayCardGA` targeting the specific enemy.
3. If auto targeting: User drops card in drop zone -> `PlayCardGA` executes targeting auto-resolved via `TargetMode`.
4. `PlayCardGA` queues `SpendManaGA` and target-effect actions. Card animates to discard pile.

**Dependencies:**
- `ManaSystem` (Requires mana validation before playing).
- `ManualTargetingSystem` / `EffectSystem` (Resolving card abilities).
- `ActionSystem` (Orchestrates all drawing, playing, and discarding events).
