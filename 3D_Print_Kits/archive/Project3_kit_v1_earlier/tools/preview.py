import numpy as np, struct, sys, os, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
def load(p):
    b=open(p,'rb').read(); n=struct.unpack('<I',b[80:84])[0]
    a=np.frombuffer(b[84:84+n*50],dtype=[('n','<f4',3),('v','<f4',(3,3)),('a','<u2')]); return a['v']
def show(ax, tris, title, offset=(0,0,0), color="#c9d3e0"):
    t=tris+np.array(offset)
    pc=Poly3DCollection(t, facecolor=color, edgecolor='none', alpha=1.0)
    n=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]); n/=np.linalg.norm(n,axis=1,keepdims=True)+1e-9
    shade=0.45+0.55*np.clip(n@np.array([0.4,-0.5,0.77]),0,1)
    base=np.array(matplotlib.colors.to_rgb(color)); pc.set_facecolor(np.clip(base*shade[:,None],0,1))
    ax.add_collection3d(pc); ax.set_title(title,fontsize=8)
D=sys.argv[1]; out=sys.argv[2]
sets=[("closed/01_Core.stl","Core (closed)"),("open/01_Core_BASE.stl","Core BASE (open)"),("open/01_Core_ROOF.stl","Core ROOF (open)"),
      ("closed/12_SleepDome_Quarters1.stl","Sleep dome (closed)"),("open/12_SleepDome_Quarters1_BASE.stl","Sleep dome BASE"),("open/12_SleepDome_Quarters1_ROOF.stl","Sleep dome ROOF"),
      ("closed/03_Module_Lab.stl","Lab module (closed, stands on end)"),("open/03_Module_Lab_HALF_print_x2.stl","Lab module HALF (print x2)"),("closed/19_Hallway_L1_N_to_R.stl","Hallway L1")]
fig=plt.figure(figsize=(15,13))
for i,(f,tt) in enumerate(sets):
    ax=fig.add_subplot(3,3,i+1,projection='3d'); tr=load(os.path.join(D,f))
    if "BASE" in f or "HALF" in f:  # cut-away view: show only the front half to reveal the inside
        pass
    show(ax,tr,tt)
    mn,mx=tr.reshape(-1,3).min(0),tr.reshape(-1,3).max(0); c=(mn+mx)/2; r=(mx-mn).max()/2
    ax.set_xlim(c[0]-r,c[0]+r); ax.set_ylim(c[1]-r,c[1]+r); ax.set_zlim(c[2]-r,c[2]+r); ax.view_init(28,-55); ax.set_axis_off()
plt.tight_layout(); plt.savefig(out,dpi=80)
