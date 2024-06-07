M98 P"/macros/work_limits_1"
; Set tool offset
G10 P1 U{-(global.x_home_offset_1 + global.x_user_offset_1 + global.x_probe_offset_1)} 
G10 P1 V{-(global.y_home_offset_1 + global.y_user_offset_1 + global.y_probe_offset_1)} 
G10 P1 Z{-(global.z_probe_correction + global.z_bed_height + global.z_user_offset_1 + global.z_probe_offset_1)}
; M567 P1 E1:{global.extruder_mixing_1 == 0 ? 1 : 0}:{global.extruder_mixing_1 == 0 ? 1 : 0}