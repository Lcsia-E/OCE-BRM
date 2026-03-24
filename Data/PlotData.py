import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.cm as cm
import matplotlib.colors as colors
import matplotlib.gridspec as gridspec

# ---------------------------------------------------
# 1. Load the dataset
# ---------------------------------------------------

# Define the file name (it must be in the same folder as your script)
file_path = 'C_RF10.csv'

# Read the CSV file, skipping the first row (usually metadata)
df = pd.read_csv(file_path, skiprows=1)

# Remove the first data row (if it contains incomplete data)
df = df.iloc[1:].reset_index(drop=True)

# ---------------------------------------------------
# 2. Clean and prepare data
# ---------------------------------------------------

# Convert text columns to numeric values (ignore errors)
df['Time'] = pd.to_numeric(df['Time'], errors='coerce')
df['Resp'] = pd.to_numeric(df['Resp'], errors='coerce')
df['Rein'] = pd.to_numeric(df['Rein'], errors='coerce')

# Remove rows that have missing values in 'Time' or 'Resp'
df = df.dropna(subset=['Time', 'Resp'])

# ---------------------------------------------------
# 3. Detect reward increases (Rein column)
# ---------------------------------------------------

# Convert 'Rein' column to an array
rein = df['Rein'].fillna(0).values

# Store indexes where reinforcement increases
increases = []
last_idx = -10  # used to avoid detecting points too close together

# Loop through data to find where reward increased
for i in range(1, len(rein)):
    # Check if reward increased and at least 4 rows have passed
    if rein[i] > rein[i - 1] and (i - last_idx) >= 4:
        increases.append(i)
        last_idx = i

# ---------------------------------------------------
# 4. Create a figure with uneven subplot sizes
# ---------------------------------------------------

# Create the overall figure
fig = plt.figure(figsize=(7, 10))

# Define grid layout with different width and height ratios
gs = gridspec.GridSpec(2, 2, width_ratios=[5, 1], height_ratios=[4, 3])

# ---------------------------------------------------
# 5. Top subplot: Participant trajectory
# ---------------------------------------------------

# Create the top plot (spans both columns)
ax1 = fig.add_subplot(gs[0, :])

# If there is a Time column, use it to color the points
if 'Time' in df.columns:
    norm = colors.Normalize(vmin=df['Time'].min(), vmax=df['Time'].max())
    cmap = cm.viridis
    
    # Draw a black line connecting points
    ax1.plot(df['X'], df['Y'], color='black', linewidth=0.5)
    
    # Draw scatter points colored by time
    scatter1 = ax1.scatter(df['X'], df['Y'], c=df['Time'], cmap=cmap, s=30, zorder=3)
    
    # Add colorbar to show the time scale
    fig.colorbar(scatter1, ax=ax1, label="Time (sec)")
else:
    # Fallback in case Time column doesn't exist
    ax1.plot(df['X'], df['Y'], color='gray', linewidth=0.5)
    ax1.scatter(df['X'], df['Y'], color='black', s=30, zorder=3)

# Add rectangles representing the lever and dispenser
lever_rect = plt.Rectangle((-.6, 0), 1.2, -.65, color='blue', alpha=0.6, label='Lever')
ax1.add_patch(lever_rect)

dispenser_rect = plt.Rectangle((-2.6, 0), .7, -.25, color='red', alpha=0.6, label='Dispenser')
ax1.add_patch(dispenser_rect)

# Customize the trajectory plot
ax1.set_title(file_path)
ax1.set_xlabel("X")
ax1.set_ylabel("Y")
ax1.set_xlim(-2.8, 2.8)
ax1.set_ylim(0, -6)
ax1.grid(False)
ax1.legend(loc='upper right', frameon=False)

# ---------------------------------------------------
# 6. Bottom-left subplot: Response rate
# ---------------------------------------------------

ax2 = fig.add_subplot(gs[1, 0])

# Plot the response rate over time
ax2.plot(df['Time'], df['Resp'], color='black', linewidth=1, label='Response Rate')

# Add vertical bars where rewards increased
ax2.scatter(df['Time'].iloc[increases], df['Resp'].iloc[increases],
            color='black', s=150, marker='|', label='Reward', zorder=5)

# Customize the response plot
ax2.set_xlabel('Time (sec)')
ax2.set_ylabel('Response Rate')
ax2.set_xticks([50, 100, 150, 200, 250])
ax2.set_yticks([0, 50, 100, 150, 200])
ax2.set_xlim(-0.5, 250)
ax2.set_ylim(-0.5, 200)
ax2.grid(False)
ax2.spines['top'].set_visible(False)
ax2.spines['right'].set_visible(False)

# ---------------------------------------------------
# 7. Bottom-right subplot (blank area)
# ---------------------------------------------------

# Create an empty subplot to balance the layout
ax_unused = fig.add_subplot(gs[1, 1])
ax_unused.axis('off')

# ---------------------------------------------------
# 8. Final layout and display
# ---------------------------------------------------

plt.tight_layout()
plt.show()



