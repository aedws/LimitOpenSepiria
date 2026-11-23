# API 가 만든 스프라이트 시트를 프레임별로 잘라, 같은 크기 칸에 가운데 맞춰 가로 한 줄로 저장합니다.
import sys
from PIL import Image
src,out,cols,rows,fw,fh=sys.argv[1],sys.argv[2],int(sys.argv[3]),int(sys.argv[4]),int(sys.argv[5]),int(sys.argv[6])
im=Image.open(src).convert('RGBA'); W,H=im.size; a=im.split()[3]
def bands(n,size,axis):
    # 내용이 있는 구간을 찾아 n 개로 묶습니다(빈 줄 기준). 못 찾으면 균등 분할.
    prof=[0]*size; px=a.load()
    for y in range(H):
        for x in range(W):
            if px[x,y]>40: prof[x if axis==0 else y]+=1
    runs=[];start=None
    for i,v in enumerate(prof+[0]):
        if v>0 and start is None: start=i
        if v==0 and start is not None: runs.append([start,i]); start=None
    # 가까운 구간끼리 합쳐 n 개가 될 때까지
    while len(runs)>n:
        gaps=[(runs[i+1][0]-runs[i][1],i) for i in range(len(runs)-1)]; g,i=min(gaps); runs[i][1]=runs[i+1][1]; del runs[i+1]
    if len(runs)!=n: return [[i*size//n,(i+1)*size//n] for i in range(n)]
    return runs
ys=bands(rows,H,1); frames=[]
for (y0,y1) in ys:
    strip=im.crop((0,y0,W,y1))
    if cols==1: frames.append(strip.crop(strip.getbbox()) if strip.getbbox() else strip); continue
    aa=strip.split()[3]; px=aa.load(); prof=[sum(1 for y in range(strip.height) if px[x,y]>40) for x in range(W)]
    runs=[];start=None
    for i,v in enumerate(prof+[0]):
        if v>0 and start is None: start=i
        if v==0 and start is not None: runs.append([start,i]); start=None
    while len(runs)>cols:
        gaps=[(runs[i+1][0]-runs[i][1],i) for i in range(len(runs)-1)]; g,i=min(gaps); runs[i][1]=runs[i+1][1]; del runs[i+1]
    if len(runs)!=cols: runs=[[i*W//cols,(i+1)*W//cols] for i in range(cols)]
    for (x0,x1) in runs:
        c=strip.crop((x0,0,x1,strip.height)); frames.append(c.crop(c.getbbox()) if c.getbbox() else c)
# 모든 프레임에 같은 축소 배율: 가장 큰 프레임이 칸에 꼭 맞게
mw=max(f.width for f in frames); mh=max(f.height for f in frames); k=min(fw/mw,fh/mh)
sheet=Image.new('RGBA',(fw*len(frames),fh),(0,0,0,0))
for i,f in enumerate(frames):
    w=max(1,round(f.width*k)); h=max(1,round(f.height*k)); s=f.resize((w,h),Image.BOX); p=s.load()
    for y in range(h):
        for x in range(w):
            r,g,b,al=p[x,y]; p[x,y]=(r,g,b,255) if al>=128 else (0,0,0,0)
    sheet.paste(s,(i*fw+(fw-w)//2,(fh-h)//2),s)
sheet.save(out); print(out,len(frames),'frames',sheet.size,'scale %.3f'%k)
