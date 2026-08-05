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

This is decision support, not a guarantee. Safety cars, traffic, weather,
damage, driver changes and future pace are uncertain. Confidence grows with the
rolling sample window, and the planner reports a learning state until sufficient
live data is available.

The model follows established circuit-racing strategy practice: discretize by
lap, combine fuel-mass and tire-degradation effects with pit losses, and compare
complete feasible strategies. Future work can add probabilistic safety-car and
traffic scenarios without replacing the deterministic planner.
