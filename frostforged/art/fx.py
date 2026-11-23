# 우주 속성 이펙트 중 코드로 그리는 것들: 참월(cut_black, cut_white, needle, ring), 섬멸·명멸의 창(spear_blue, spear_red).
# 프레임을 가로로 이어 붙인 한 줄로 저장합니다. (void.png, ignorance.png 는 이미지 생성 API 로 만든 뒤 fx_slice.py 로 잘랐습니다.)
from PIL import Image
import random, sys
out=sys.argv[1]
W=(255,255,255,255); G=(214,220,236,255)

# 참월: 화면을 가르는 면도날 같은 직선. 처음엔 또렷한 선(가운데 심 + 양쪽 테), 이어서 가는 선, 마지막엔 점선으로 끊기며 사라집니다.
def cut(name,core,rim,fade):
    fw,fh,n=96,8,12; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    for f in range(n):
        for x in range(fw):
            X=f*fw+x
            if f<5:                                   # 또렷한 선: 테-심-테
                px[X,cy-1]=rim; px[X,cy]=core; px[X,cy+1]=rim
                if f==0: px[X,cy-2]=rim[:3]+(110,); px[X,cy+2]=rim[:3]+(110,)   # 긋는 순간 한 겹 더 번쩍
            elif f<8:                                 # 가는 선
                px[X,cy]=fade[:3]+((200,150,110)[f-5],)
            else:                                     # 점선(갈수록 듬성듬성)
                period=(6,8,10,12)[f-8]; on=(4,4,3,2)[f-8]
                if x%period<on: px[X,cy]=fade[:3]+((120,95,70,45)[f-8],)
    sheet.save(out+name+'.png')

# 파열: 양 끝이 뾰족한 흰 바늘(렌즈 모양)이 번쩍였다가 희미해지며 점선으로 흩어집니다.
def needle():
    fw,fh,n=96,12,6; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    half=(4,3,2,1,0,0); alpha=(255,255,200,140,110,60)
    for f in range(n):
        for x in range(fw):
            t=x/(fw-1); k=1-abs(2*t-1)**1.6
            h=int(round(half[f]*k))
            if f>=4 and x%(8 if f==4 else 12)>=(5 if f==4 else 3): continue
            for y in range(cy-h,cy+h+1): px[f*fw+x,y]=(255,255,255,alpha[f])
    sheet.save(out+'needle.png')

# 충격 고리: 가는 흰 원이 퍼져 나갑니다.
def ring():
    import math
    fw,n=48,5; sheet=Image.new('RGBA',(fw*n,fw),(0,0,0,0)); px=sheet.load(); c=(fw-1)/2
    for f in range(n):
        r=5+f*4.5; a=(255,230,180,120,60)[f]
        for y in range(fw):
            for x in range(fw):
                d=math.hypot(x-c,y-c)
                if abs(d-r)<0.6: px[f*fw+x,y]=(255,255,255,a)
    sheet.save(out+'ring.png')

def spear(name,pal):
    # pal: 윤곽, 어두운 색, 중간 색, 밝은 색, 흰색
    O,D,M,L,Wh=pal; fw,fh,n=56,16,4; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2; rnd=random.Random(3)
    head=["....WW......","..WWLLWW....","WWLLLMMMWW..","WLLLMMMMDDWW","WWLLLMMMWW..","..WWLLWW....","....WW......"]   # 오른쪽을 향한 창날(끝이 오른쪽)
    for f in range(n):
        body={}
        for x in range(14,43): body[(x,cy)]=M if (x+f)%5 else L          # 자루
        for x in range(14,43,6): body[(x,cy)]=D
        for j,row in enumerate(head):                                    # 창날
            for i,ch in enumerate(row):
                if ch!='.': body[(42+i,cy-3+j)]={'W':Wh,'L':L,'M':M,'D':D}[ch]
        for x in range(2,15):                                            # 꼬리(프레임마다 일렁임)
            spread=(15-x)//4
            for dy in range(-spread,spread+1):
                if rnd.random()<0.55-0.03*(15-x)+(0.25 if dy==0 else 0): body[(x+rnd.randrange(0,2),cy+dy)]=rnd.choice((L,M,M,D))
        for (x,y) in list(body):
            for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                q=(x+dx,y+dy)
                if q not in body and 0<=q[0]<fw and 0<=q[1]<fh and x>=12: px[f*fw+q[0],q[1]]=O
        for (x,y),c in body.items():
            if 0<=x<fw and 0<=y<fh: px[f*fw+x,y]=c
    sheet.save(out+name+'.png')

cut('cut_black',(14,10,24,255),(255,255,255,255),(236,236,246,255))
cut('cut_white',(255,255,255,255),(151,199,254,255),(190,214,255,255))
needle()
ring()
spear('spear_blue',((20,30,70,255),(59,81,130,255),(100,153,232,255),(170,226,255,255),(255,255,255,255)))
spear('spear_red',((70,20,24,255),(183,50,39,255),(237,120,46,255),(255,214,110,255),(255,255,255,255)))
