# User Manual

Please get in touch, if the information is not sufficient or you encounter any other problem.

## A) Configuration

For now, some configurations have to be done in config files before an experiment can be analyzed.
* InputData.conf (A.1, A.2)
* ExperimentSetup.conf (A.3)
* EyeTargets.conf (A.4)

### A.1) Data Column Names

_Firstly_, check the column names of your csv/tsv file and adjust the __InputData.conf__ file accordingly. It consists of key value pairs. The key (left) is predefined and the right hand side reflects the column name of the input data file, e.g:
```
...
EyePositionLeftX = Eye position left X (RCSmm)
EyePositionLeftY = Eye position left Y (RCSmm)
EyePositionLeftZ = Eye position left Z (RCSmm)
...
```

Note that there are some data preparation tools for reshaping the csv/tsv data gathered in the main menu: DataPrep. That section will further be extended dependent on the need. It e.g. provides a tool to fix wrong numbers of tabulators, when text labels where inserted to the eye tracking data from a different software controlling the experiment run.

### A.2) Target Split Strategy

_Secondly,_ check on the strategy to define the target events. Either a time stamped text label inserted into the measurement can be used, or the names of presented media files can be used directly. In the first case, the two key value pairs, also in the __InputData.conf__ have to be setup. 
```
...
StatesExperiment = Instructions; start; Trial Start
StatesTarget = Target; leftTarget; rightTarget
...
```
Those labels have to be present in the input csv/tsv file and are then used to split the target views and to define the starting points for the reaction time computations.

For the second case, those key value pairs have to be left empty. Then, the switch of the presented targets defines a split. A target is configured in __EyeTargets.conf__ and the related presented media (image) in ___Media.conf__; see A.4).

### A.3) Coordinate Mapping and Device Name

_Thirdly_, the spatial parameters of the setup can be changed. The deviation to the screen target to the gaze point is analyzed and viewed in degrees. Therefore, the screen position of the gaze point has to be transformed. The key value pairs in __ExperimentSetup.conf__ provide the coordinate transformation of the gaze point on screen to 3D angles. The gaze point is represented in any coordinate system/measures as ouput by the eye tracker. The linear transformation depends on the distance and position of the head to the screen and the eye trackers coordinate system. The provided values are setup for the _Tobii TX-300_ with the head at 60 cm distance from the device. 
```
xk =  3.74205273
xd = -0.752033961
yk =  3.77777463
yd = -7.566204361
Device = "Tobii"
```


### A.4) Target Positions and Related Media Names

_Forthly_, the presented target name and positions (in pixels) have to be setup. Each presented media (image) is identified by its name and related to the configured target. The default csv files __EyeTargets.conf__ and __Media.conf__can be modified to do so:

__EyeTargets.conf__
```
TargetName	PositionXPx	PositionYPx	Diameter
start	960	540	30
left	260	540	30
right	1660	540	30
off 	960	540	30
post	960	540	30
```

__Media.conf__
```
MediaName	TargetName	TargetNr
CueLf.jpg	left	1
CueLs.jpg	left	1
CueRf.jpg	right	2
CueRs.jpg	right	2
TargetStart.jpg	start	0
Target_lif.jpg	left	1
Target_lis.jpg	left	1
Target_ref.jpg	right	2
Target_res.jpg	right	2
Targetf.jpg	off	3
Targets.jpg	off	3
```

## B) Analyzes

### B.1) Visualize and Verify Data

<img width="874" alt="SPBView" src="https://github.com/gileoo/SPBView/assets/17740998/e1d3dd0d-e5b6-47a3-9586-5dc1f9679847">

SPBView provides the above interactive visualization to verify and correct saccadic data. The horizontal error of the gaze point is drawn as a blue line, the vertical as orange line. Eye movements are categorized in fixation (green), pro-saccadic (yellow), and anti-saccadic (blue) movement intervals. The duration of the interval is shown. The grey/black bar plot illustrates the speed of the movement. The reaction time is shown by the black bars of the speed plot.

The axes can be adjusted using the mouse wheel; also separated for each axis when hovering over the specific axis. For panning hold down the right mouse button.

Navigating to targets can be executed by keypresses, e.g.: _space_ (next) and _backspace_ (previous). Note, that the focus has to be on the gaze plot (may need a mouse click in that area before). The next/pervious actions take the _Filter_ field into account. This is a space separated white list of substrings of target names to be shown;
e.g. 'Left Right' will enable a filter that target navigation is limited to those targets having 'Left' or 'Right' in its name (case sensitive). More navigation commands and hotkeys can be found in the __Target__ section of the main menu.

Alternatively, the panel with the colored small rectangles on top of the gaze plot can be used by mouse clicks. Each rectangle related to one split target interval. The actually shown target is colored in black. This is useful to quickly jump over longer time intervals of the data. Note, that when holding down the _shift_ key, the rectangular visualization is warped, such that small rectangles are enlarged under the current mouse position.

On top of the target navigation view the blink view shows all blink events as black interval and black to white colormapped circle. The vertical position and color of the circle indicates the length of the blink. The higher and whiter the longer. The circle can be clicked on to show details. 

The __reaction time__ detection can be altered by adjusting the _SplitThrs_ value (split threshold, default 0.3). If increased the time position of fixation and saccade start will be shifted backwards in time, as a looser threshold will trigger the split. This might be helpful to adjust for more jittered data. 

If the reaction time is detected wrongly, it can be adjusted manually by holding the _alt_ key and dragging along the horizontal limits in the gaze plot; E.g. press&hold alt, click at the very left of the target view and move to the time value of the start of the saccade and release. The black bars recolor accordingly, and the hole frame will be marked light blue, such that manual corrections become immediately visible; see image below. Here, the reaction time was extended to the anti-saccade manually; for demonstration. An ASCII file with the manual corrections is generated at the folder of the data file, using its original name with extension: __.corr__ .

![Screenshot 2024-07-15 130537](https://github.com/user-attachments/assets/a4deae60-d36f-4009-a59b-101a3c2ef114)

Full targets can further by classifier by eight distinct error classes (user defined). Bad data may be label automatically or can be done so manually; see main menu _Target_ for the hotkeys.

### B.2) Export Analyzes

When all target data was double checked, classified, and maybe manually corrected, the analysis is ready to be exported; in the main menu:
``` 
    Analysis > SaveAnalysis
```
A save file dialog enables to define the locate an name of the spread sheet output as .xslx file; to be opened in e.g. excel.

Note, that there are the two sheets __GazeMovement__ and __Blinks__ with related tables. One field with in table statistics is not automatically updated at the moment. To trigger the computation of the standard deviation one has to click into the cell and press ctrl-shift-enter. Array type equations are currently not updated automatically in excel on file open.

The analysis menu provides two more options:
``` 
    Analysis > SaveBlinkAnalysis
```
To create an output file solely related to the detected blinks and
``` 
    Analysis > ShowBlinks
```
to create a time-wise aligned overview figure of blinks of several participants. The output looks like the blink overview panel in the standard view, but arranges multiple participants on top of each outer for visual comparison.

