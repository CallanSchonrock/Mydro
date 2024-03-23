import tkinter as tk
from tkinter import filedialog
from tkinter import ttk
import pandas as pd
import matplotlib.pyplot as plt

class CheckListbox(tk.Frame):
    def __init__(self, master, options, **kwargs):
        super().__init__(master, **kwargs)
        self.options = options
        self.vars = [tk.BooleanVar(value=False) for _ in range(len(self.options))]
        self.canvas = tk.Canvas(self)
        self.scrollbar = ttk.Scrollbar(self, orient="vertical", command=self.canvas.yview)
        self.inner_frame = tk.Frame(self.canvas)
        self.canvas.create_window((0, 0), window=self.inner_frame, anchor="nw")
        
        for i, option in enumerate(self.options):
            tk.Checkbutton(self.inner_frame, text=option, variable=self.vars[i]).pack(anchor=tk.W)
        
        self.inner_frame.bind("<Configure>", lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.canvas.configure(yscrollcommand=self.scrollbar.set)
        
        self.canvas.pack(side="left", fill="both", expand=True)
        self.scrollbar.pack(side="right", fill="y")

    def checked_items(self):
        return [self.options[i] for i, var in enumerate(self.vars) if var.get()]

class CSVPlotter:
    def __init__(self, master):
        self.master = master
        self.master.title("Select Columns to Plot")

        # Button to load CSV file
        self.load_button = tk.Button(self.master, text="Load CSV", command=self.load_csv)
        self.load_button.grid(row=0, column=0, columnspan=2, pady=5)

        # CheckListbox to select index column
        self.index_checklistbox = CheckListbox(self.master, options=[], bd=0)
        self.index_checklistbox.grid(row=1, column=0, padx=5, sticky="nsew")

        # CheckListbox to select value columns
        self.value_checklistbox = CheckListbox(self.master, options=[], bd=0)
        self.value_checklistbox.grid(row=1, column=1, padx=5, sticky="nsew")

        # Button to plot selected columns
        self.plot_button = tk.Button(self.master, text="Plot Selected Columns", command=self.plot_columns)
        self.plot_button.grid(row=2, column=0, columnspan=2, pady=5)

        # Configure grid row and column weights
        self.master.grid_rowconfigure(1, weight=1)
        self.master.grid_columnconfigure(0, weight=1)
        self.master.grid_columnconfigure(1, weight=1)

    # Function to load CSV file
    def load_csv(self):
        file_path = filedialog.askopenfilename()
        if file_path:
            try:
                self.df = pd.read_csv(file_path, skiprows=4)
                column_names = self.df.columns.tolist()
                self.index_checklistbox.destroy()
                self.index_checklistbox = CheckListbox(self.master, options=column_names, bd=0)
                self.index_checklistbox.grid(row=1, column=0, padx=5, sticky="nsew")

                self.value_checklistbox.destroy()
                self.value_checklistbox = CheckListbox(self.master, options=column_names, bd=0)
                self.value_checklistbox.grid(row=1, column=1, padx=5, sticky="nsew")
            except Exception as e:
                print("Error loading CSV file:", e)

    # Function to plot selected columns
    def plot_columns(self):
        index_columns = self.index_checklistbox.checked_items()
        value_columns = self.value_checklistbox.checked_items()
        if index_columns and value_columns:
            plt.cla()
            plt.xlabel(', '.join(index_columns))
            plt.ylabel('Values')
            plt.title('Plot of Selected Columns')
            for col in value_columns:
                plt.plot(self.df[index_columns], self.df[col], label=col)
            plt.legend()
            plt.show()
        else:
            print("Select at least one index column and one value column to plot.")

if __name__ == "__main__":
    root = tk.Tk()
    app = CSVPlotter(root)
    root.mainloop()