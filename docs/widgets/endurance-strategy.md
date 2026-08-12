# Endurance strategy planner

The Fuel / Virtual Energy widget learns recent fuel use, Virtual Energy use,
lap pace and maximum tire wear once per completed valid lap. It then evaluates
complete strategies against the remaining race or session horizon.

The estimate includes current fuel range, full-tank range, a configurable stint
limit, reserve fuel, stationary/pit-lane loss, tire-change time, measured tire
wear and the configured tire allocation. Practice and qualifying still receive
a complete strategy simulation so they can be used to prepare for the event;
for a timed session, however, the displayed plan duration is always the actual
remaining session time. A recent pace trend is never compounded across every
future lap because that can turn a six-hour horizon into a fictitious seven-hour
plan.

## Full-push and fuel-save plans

The first strategy box is the deterministic `FULL PUSH` baseline:

- the opening stint uses the fuel already in the car;
- intermediate stints are full or near-full tank stints to minimize stops;
- the final stop adds only the fuel required for the final stint plus the
  configured safety reserve. It never fills the tank when only a short final
  stint remains;
- the equivalent final Virtual Energy target is shown when a stable live usage
  sample is available.

The second box is an independent `FUEL SAVE` alternative. It searches for a
lower per-lap fuel target, capped at 15% saving, that either removes a stop or
extends the current stint. It reports the target consumption, pit sequence and
the tire plan for that alternative. It does not merely rename a weather or
flag scenario.

Tire recommendations use measured wear progression rather than a single life
percentage. At least three valid wear samples are required before projecting a
change, and 85% tire life remaining is not itself a reason to replace a set.
The configured wear threshold, available allocation and the ability to
double-stint remain constraints.

## Live contingency scenarios

The deterministic green-flag plan remains the baseline. Three separate lines
provide decision support without presenting uncertain events as facts:

- `FLAGS` treats yellow as an incident warning only. It never assumes a Safety
  Car, a speed reduction or a discounted pit loss, so the normal green-pace
  strategy remains active;
- `WEATHER` uses live rain intensity, track wetness and the rolling rain trend
  to distinguish stable dry running, light rain, a preparation window and a
  heavy-rain wet-tire window;
- `TRAFFIC` uses the official gaps ahead and behind to identify an undercut,
  possible one-lap extension or the original clean-air window.

These scenarios are explainable heuristics around the optimized stint plan.
The architecture can later replace their fixed assumptions with a Monte Carlo
distribution while retaining the same UI contract.

This is decision support, not a guarantee. Traffic, weather, damage, driver
changes and future pace are uncertain. Confidence grows with the
rolling sample window, and the planner reports a learning state until sufficient
live data is available.

The model follows established circuit-racing strategy practice: discretize by
lap, combine fuel-mass and tire-degradation effects with pit losses, and compare
complete feasible strategies.

Primary implementation basis:

- the project research brief `Pesquisa_Estrategias_Endurance_SimRacing(1).md`,
  particularly its minimum-versus-safe lap model, full intermediate stints,
  exact final splash, fuel-save alternative and sequential LMU service model;
- the official LMU [MFD guide](https://guide.lemansultimate.com/hc/en-gb/articles/13202210967055-Understanding-the-MFD-Multi-Function-Display),
  which exposes pit lap, fuel/Virtual Energy and tire service controls;
- the official LMU [Virtual Energy guide](https://guide.lemansultimate.com/hc/en-gb/articles/13152376674191-What-is-Virtual-Energy-NRG)
  and [limited tyre guide](https://guide.lemansultimate.com/hc/en-gb/articles/13210731599119-What-are-limited-tyres).

Supporting research:

- Carrasco Heine and Thraves, [On the Optimization of Pit-Stop Strategies via
  Dynamic Programming](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3769652),
  treats tire wear and weather as strategy inputs. Its Safety Car assumptions
  are intentionally not applied to LMU;
- Aguad and Thraves, [Optimizing pit stop strategies in Formula 1 with dynamic
  programming and game theory](https://www.sciencedirect.com/science/article/pii/S0377221724005484),
  demonstrates why competitor interaction cannot be ignored by a pit model.
