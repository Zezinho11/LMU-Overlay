# Endurance strategy planner

The Fuel / Virtual Energy widget learns recent fuel use, Virtual Energy use,
lap pace, pace trend and maximum tire wear once per completed lap. It then
enumerates feasible balanced stints and ranks them by projected total race
time.

The estimate includes current fuel range, full-tank range, a configurable stint
limit, reserve fuel, stationary/pit-lane loss, tire-change time, measured tire
wear and the configured tire allocation. The UI shows the preferred stint/stop
count, pit laps and fuel targets, tire-change laps and the next-best feasible
alternative.

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

Research basis:

- Carrasco Heine and Thraves, [On the Optimization of Pit-Stop Strategies via
  Dynamic Programming](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=3769652),
  treats tire wear and weather as strategy inputs. Its Safety Car assumptions
  are intentionally not applied to LMU;
- Aguad and Thraves, [Optimizing pit stop strategies in Formula 1 with dynamic
  programming and game theory](https://www.sciencedirect.com/science/article/pii/S0377221724005484),
  demonstrates why competitor interaction cannot be ignored by a pit model.
