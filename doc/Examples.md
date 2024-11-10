# Example Setup 1
This is a short step by step guidance to view and analyze the recording stored in:  
```
exampleData/example01-TimeLabels.tsv
```

## 1) Configure header names

SPBView openss .tsv or .csv files. To read in the required values, the names of the headers must be provided in a config file. 
Please open the first lines of the __.tsv__ file (e.g. as a spreadsheet) as well as the __InputData.conf__ in a text editor of your choice: VS code, vim, or also Notepad will do the job.
Go through all the data value header names, e.g. top down in the __.conf__ file and ensure that the text matches the related column headers.

Data file header:
![image](https://github.com/user-attachments/assets/fc33e94a-3954-4b97-bb35-3a124b43d7d1)

__InputData.conf__
```
RecordingTimestamp = TimeStamp
PupilDiameterLeft = PupilSizeLeft
PupilDiameterRight = PupilSizeRight
GazePointX = GazePointX
GazePointY = GazePointY
GazePointLeftX = GazePointXLeft
GazePointLeftY  = GazePointYLeft
GazePointRightX = GazePointXRight
GazePointRightY = GazePointYRight
ValidityLeft = ValidityLeft
ValidityRight = ValidityRight
EyePositionLeftX = user-const 228.1
EyePositionLeftY = user-const 208.0
EyePositionLeftZ = user-const 599.5
EyePositionRightX = user-const 289.9
EyePositionRightY = user-const 206.5
EyePositionRightZ = user-const 603.2
Event = Event
StatesTargetType = fearful; traurig; happy; neutral; sad; arousal; positive
StatesExperiment = Instructions; start; Trial Start
StatesTarget = Trial start; leftTarget; rightTarget; Target Off
StatesEyeMovement = anti; pro
```

## 2) Configure Targets
__EyeTargets.conf__
```
TargetName	PositionXPx	PositionYPx	Diameter
start	960	540	30
left	260	540	30
right	1660	540	30
off	960	540	30
```
Provide the target positions in pixel space of the shown target images. Each target type will split into the viewed time interval. Note, that the ordering in the __StateTarget__ above must follow the ordering of the lines in the __EyeTargets.conf__.

## 3) Open SPB View

Select the provided example file __example01-TimeLabels.tsv__. Or open it via the main menu: _File>Open_.

 ![image](https://github.com/user-attachments/assets/161f597f-84fa-453b-a7bc-7423c459cc40)

 Click once into the gaze view to focus the context there. Next, e.g. press fice times _space_ to navigate forwards time-wise to a left target event.

 ![image](https://github.com/user-attachments/assets/3779e3c2-f2e5-45a6-818a-9204c2a405c6)

 Adjust the input field _Filter_ by 'left right'. To filter for all target containing 'left' or 'right' in its name. Then when pressing _space_/_backspace_ those targets will be visited only.

 Hold down the _shift_ key when hovering over the navgation bar between the blinks and the gaze view to adjust the scaling for the target recangles. Click on a colored rectangle to jump to that target.
 
 ![image](https://github.com/user-attachments/assets/13dbbe16-ada1-41e1-b768-fe5173482ca8)

## 4) Correct Reaction Time or Error-Classify Targets

 Control all the targets of interst and manually adjust by either correcting the automatically detected reaction time, or label the whole target via an error class (_ctrl-1_ to _ctrl-0_); or as _bad data_ (_ctrl-B_). The reaction time is corrected by holding the _ctrl_ key and dragging in the gazew view from start to end (or vice versa); for the time coordinates (x-axis).

![image](https://github.com/user-attachments/assets/138797bf-8bdd-4e97-af86-6a66fd23a417)

If the reaction time was adjusted manually, the frame is colored in lila. For error classes colors from red to yellow are used.

![image](https://github.com/user-attachments/assets/cfa3e14e-68e9-4409-bed5-690e129ea7aa)

## 5) Export Analysis

Error classes are excluded in the analysis table output. Manual reaction time are used instead of the auto detected ones. Further, only target are exported that are white-listed in the _Filter_.
Export the analysis via the main menu: _Analysis>SaveAnalysis_.

![image](https://github.com/user-attachments/assets/95738e8e-a627-478c-99cb-4fc528b58c31)

Note that some rows in the header are array-entered equations. To update them, they have to be focused in the _fx_ field and _shift-ctlr-enter_ has to be pressed.

