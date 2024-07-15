# User Manual

## Configuration

For now, some configurations have to be done in config files before an experiment can be analyzed.

_First_ check the column names of your csv/tsv file and adjust the __InputData.conf__ file accordingly. It consists of key value pairs. The key (left) is predefined and the right hand side reflects the column name of the input data file, e.g:
```
...
EyePositionLeftX = Eye position left X (RCSmm)
EyePositionLeftY = Eye position left Y (RCSmm)
EyePositionLeftZ = Eye position left Z (RCSmm)
...
```

Note that there are some data preparation tools for reshaping the csv/tsv data gathered in the main menu: DataPrep. That section will further be extended dependent on the need. It e.g. provides a tool to fix wrong numbers of tabulators, when text labels where inserted to the eye tracking data from a different software controlling the experiment run.

_Second_ check on the strategy to define the target events. Either a time stamped text label inserted into the measurement can be used, or the names of presented media files can be used directly. In the first case, the two key value pairs, also in the __InputData.conf__ have to be setup. 
```
...
StatesExperiment = Instructions; start; Trial Start
StatesTarget = Target; leftTarget; rightTarget
...
```
Those labels have to be present in the input csv/tsv file and are then used to split the target views and to define the starting points for the reaction time computations.

