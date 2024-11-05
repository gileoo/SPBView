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
ParticipantName = Participant name
ReactionTimeStart = ReactionTimeStart
ReactionTimeStop = ReactionTimeStop
TargetNr = TargetNr
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
EyePositionLeftX = Eye position left X (RCSmm)
EyePositionLeftY = Eye position left Y (RCSmm)
EyePositionLeftZ = Eye position left Z (RCSmm)
EyePositionRightX = Eye position right X (RCSmm)
EyePositionRightY = Eye position right Y (RCSmm)
EyePositionRightZ = Eye position right Z (RCSmm)
EyeMovementType = Eye movement type
GazeEventDuration = Gaze event duration
Event = Event
PresentedMediaName = Presented Media name
AoiHitCueR_Rectangle2 = AOI hit [CueR (4).jpg - Rectangl e 2]
ErrorType = ErrorType
StatesTargetType = fearful; traurig; happy; neutral; sad; arousal; positive
StatesExperiment = Instructions; start; Trial Start
StatesTarget = Target; leftTarget; rightTarget
StatesEyeMovement = anti; pro
```
