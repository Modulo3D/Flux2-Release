G91 

M913 U80  				
M400 	

G1 H1 U-400 F1500

M913 U100  		
M400

G1 H1 U0.5 F50
G1 H1 U-10 F35

M400
if !sensors.endstops[3].triggered
	M112

G90
