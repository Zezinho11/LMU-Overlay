# Race Control and car status

The independent Race Control widget combines official, read-only LMU fields that
can require immediate attention:

- outstanding penalty count;
- pit request/entry/stop/exit and current lap validity;
- global phase plus the player's primary flag;
- overheating, detached body parts, dent severity, flat or detached wheels;
- last impact time and magnitude;
- pit limiter and DRS state.

The widget never invents a penalty type or repair time that LMU does not expose
through the verified binding. Warning and critical states are deterministic and
covered by renderer-independent tests.
