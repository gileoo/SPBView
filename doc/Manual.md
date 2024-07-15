# User Manual

## I) Configuration

For now, some configurations have to be done in config files before an experiment can be analyzed.

### 1) Data Column Names

_Firstly_, check the column names of your csv/tsv file and adjust the __InputData.conf__ file accordingly. It consists of key value pairs. The key (left) is predefined and the right hand side reflects the column name of the input data file, e.g:
```
...
EyePositionLeftX = Eye position left X (RCSmm)
EyePositionLeftY = Eye position left Y (RCSmm)
EyePositionLeftZ = Eye position left Z (RCSmm)
...
```

Note that there are some data preparation tools for reshaping the csv/tsv data gathered in the main menu: DataPrep. That section will further be extended dependent on the need. It e.g. provides a tool to fix wrong numbers of tabulators, when text labels where inserted to the eye tracking data from a different software controlling the experiment run.

### 2) Target Split Strategy

_Secondly,_ check on the strategy to define the target events. Either a time stamped text label inserted into the measurement can be used, or the names of presented media files can be used directly. In the first case, the two key value pairs, also in the __InputData.conf__ have to be setup. 
```
...
StatesExperiment = Instructions; start; Trial Start
StatesTarget = Target; leftTarget; rightTarget
...
```
Those labels have to be present in the input csv/tsv file and are then used to split the target views and to define the starting points for the reaction time computations.

For the second case, those key value pairs have to be left empty. Then, the switch of the presented medial file defines a split.

### 3) Coordinate Mapping and Device Name

_Thirdly_, the spatial parameters of the setup can be changed. The deviation to the screen target to the gaze point is analyzed and viewed in degrees. Therefore, the screen position of the gaze point has to be transformed. The key value pairs in __ExperimentSetup.conf__ provide the coordinate transformation of the gaze point on screen to 3D angles. The gaze point is represented in any coordinate system/measures as ouput by the eye tracker. The linear transformation depends on the distance and position of the head to the screen and the eye trackers coordinate system. The provided values are setup for the _Tobii TX-300_ with the head at 60 cm distance from the device. 
```
xk =  3.74205273
xd = -0.752033961
yk =  3.77777463
yd = -7.566204361
Device = "Tobii"
```


### 4) Target Positions and Related Media Names

_Forthly_, the presented media prefix and according target positions (in pixels) have to be setup. Each presented media (image) is identified by its prefix, which has to be contained in the name of the presented media name: e.g. 'LeftTarget.png' as media name and 'Left' as predix for identification. The default csv file __EyeTargets.conf__ can be modified to do so:
```
Prefix	PositionXPx	PositionYPx	Diameter
start	960	540	30
Left	260	540	30
Right	1660	540	30
Off 	960	540	30
post	960	540	30
```

## II) Analyzes

### 1) Visualize and Verify Data

### 2) Export Analyzes

When all target data was double checked, classified, and maybe manually corrected, the analysis 