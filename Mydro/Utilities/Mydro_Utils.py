import pandas as pd
import os
from scipy.interpolate import RectBivariateSpline
import numpy as np
import matplotlib.pyplot as plt
import csv


def rank_flows(output_directory, subcat_columns, aeps, durations, ensembles, plot_ranks=True):

    """
    Rank flows based on the provided parameters and optionally create box plots.

    Parameters:
        output_directory (str): Path to the directory containing the output files.
        subcat_cols (list): List of subcategory columns.
        aeps (list): List of AEP values.
        durations (list): List of durations.
        ensembles (list): List of ensemble values.
        plot_ranks (bool, optional): Whether to create box plots. Defaults to True.
    """
    
    # Loop through all subcatchments to be ranked for peak flows
    for subcat in subcat_columns:
                    
        with (open(os.path.join(output_directory, f'_{subcat}_RankedFlows.csv'), 'w', newline='') as f,
              open(os.path.join(output_directory, f'_{subcat}_RankedEnsembles.csv'), 'w', newline='') as g):
            rankedQWriter = csv.writer(f)
            rankedEWriter = csv.writer(g)
            for aep in aeps:
                boxPlotData = []
                for dur in durations:
                    vals = []
                    for ens in ensembles:
                        df = pd.read_csv(os.path.join(output_directory, f'{aep}_{dur}_{ens}.csv'), skiprows=4)
                        maxVal = df[subcat].max()
                        vals.extend([maxVal])

                    rankedQWriter.writerow([subcat,aep,dur] + sorted(vals))
                    rankedEnses = [ensembles[vals.index(qVal)] for qVal in sorted(vals)]
                    rankedEWriter.writerow([subcat,aep,dur] + rankedEnses)
                    boxPlotData.append(vals)
                
                if plot_ranks:
                    plt.clf()
                    plt.boxplot(boxPlotData)
                    plt.xlabel('Durations')
                    plt.xticks(range(1, len([dur[1] for dur in durations]) + 1), [dur[1] for dur in durations]) 
                    plt.ylabel('Cumecs')
                    plt.title(aep)
                    plt.savefig(os.path.join(output_directory, f'_{subcat}_{aep}_QBoxPlot.png'))


def interpolate_2d(points, x, y, kx=1, ky=1):
    """
    Perform 2D interpolation using bilinear interpolation method.

    Parameters:
        points (list of tuples): List of tuples containing (x, y, value) points.
        x (float): x-coordinate for interpolation.
        y (float): y-coordinate for interpolation.
        kx (int, optional): Order of interpolation along the x-direction. Defaults to 1.
        ky (int, optional): Order of interpolation along the y-direction. Defaults to 1.

    Returns:
        float: Interpolated value at the given (x, y) coordinate.
    """
    # Extract x, y, and values from the points
    x_points, y_points, values = zip(*points)

    # Convert to arrays
    x_points = np.array(x_points)
    y_points = np.array(y_points)
    values = np.array(values)

    # Perform bilinear interpolation
    interp_func = RectBivariateSpline(np.unique(y_points), np.unique(x_points), values.reshape(len(np.unique(y_points)), len(np.unique(x_points))), kx=kx, ky=ky)
    interpolated_value = interp_func(y, x)

    return interpolated_value

# rank_flows example usage
# path = r"E:\Python\QGIS\PyCatch\Mydro\Examples\EPR\run\output"
# subcat_cols = ["Q_36", "Q_1"]

# aeps = ['063','001']
# durs = ['015m','030m','045m','060m','090m','120m','180m','270m','360m','540m','720m','1080m','1440m']
# enses = ['E0','E1','E2','E3','E4','E5','E6','E7','E8','E9']

# rank_flows(path, subcat_cols, aeps, durs, enses, plot_ranks=True)


# Example usage
points = [(0, 0, 1), (1, 0, 2), (0, 10, 3), (1, 10, 4)]  # Example points
x = 0.5  # Example x-coordinate for interpolation
y = 0.5  # Example y-coordinate for interpolation

interpolated_value = interpolate_2d(points, x, y)
print("Interpolated value at (0.5, 0.5):", interpolated_value)