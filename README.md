# SE_Conveyors
Space Engineers PB script to analyze conveyor network.

Lists Inventory blocks and their conveyor connections.

### Installation:

1. To use Scripts you need to enable Experimental mode
2. Subscribe to the [script](https://steamcommunity.com/sharedfiles/filedetails/?id=2939076864).
3. Build a Programmable block (PB).
4. In the control menu select 'Edit' in the PB.
5. Click Browse scripts in the Edit form (Lower right)
6. Select the script. (Double click LMB)
7. Click 'Run' in the PB.
8. Build an LCD panel for display.
9. Add an INI format configuration in the LCD Custom Data.

#### LCD Custom Data
```ini
[Conveyor]

; For blocks with multible displays select which to use
display=0

; Foreground
color=FF4500

scale=0.5

skip=10

; Skip fully connected
show_all=false

; Display the construction connected via a Connector with ConnectorNamePart in the name
via=ConnectorNamePart

[Conveyor_Display0]
; short form
`
```

### Example output

![Test Grid Output](TestGridOutput.png)
#### Explanation of the displayed symbols for conveyors:

![Double](double.png) Double lined is Large Port Conveyors.


![Single](single.png) Single lined is Small Port Conveyors.

Missing line is Isolation


