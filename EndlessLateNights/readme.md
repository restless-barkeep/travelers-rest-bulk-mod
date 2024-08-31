# EndlessLateNights

This mod will move the Clock backwards a customizable amount of time when the clock reaches a customizable time.

#### config showing going from 2:30 am to 0:30 am causing EndlessLateNights!

> WARNING: do not have time warp change the current day.

```yaml
## Settings file was created by plugin rbk-tr-EndlessLateNights v0.0.3
## Plugin GUID: rbk-tr-EndlessLateNights

[EndlessLateNights]

## Hour to trigger the time loop
# Setting type: Int32
# Default value: 2
trigger hour = 2

## Min to trigger the time loop
# Setting type: Int32
# Default value: 30
trigger min = 30

## Hours to go backwards (WARNING: going back to the previous day can mess with things)
# Setting type: Int32
# Default value: 2
hours to go back = 2
```