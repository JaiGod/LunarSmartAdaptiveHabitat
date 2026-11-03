import numpy as np, struct, sys, os, json, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
def load(p):
    b=open(p,'rb').read(); n=struct.unpack('<I',b[80:84])[0]
    return np.frombuffer(b[84:84+n*50],dtype=[('n','<f4',3),('v','<f4',(3,3)),('a','<u2')])['v']
def draw(ax, t, title):
    n=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]); n/=np.linalg.norm(n,axis=1,keepdims=True)+1e-9
    shade=0.42+0.58*np.clip(n@np.array([0.4,-0.5,0.77]),0,1)
    pc=Poly3DCollection(t, edgecolor='none'); pc.set_facecolor(np.clip(np.array([0.79,0.83,0.88])*shade[:,None],0,1)); ax.add_collection3d(pc)
    mn,mx=t.reshape(-1,3).min(0),t.reshape(-1,3).max(0); c=(mn+mx)/2; r=(mx-mn).max()/2*0.95
    ax.set_xlim(c[0]-r,c[0]+r); ax.set_ylim(c[1]-r,c[1]+r); ax.set_zlim(c[2]-r,c[2]+r); ax.view_init(26,-55); ax.set_axis_off(); ax.set_title(title,fontsize=9)
root=sys.argv[1]; folder=sys.argv[2]; picks=sys.argv[3].split('|'); out=os.path.join(root,folder,'00_PREVIEW.png')
cols=3; rows=(len(picks)+cols-1)//cols
fig=plt.figure(figsize=(15,4.3*rows))
for i,p in enumerate(picks):
    f,title=p.split('=')
    ax=fig.add_subplot(rows,cols,i+1,projection='3d'); draw(ax, load(os.path.join(root,folder,f)), title)
plt.tight_layout(); plt.savefig(out,dpi=72); print(out)
