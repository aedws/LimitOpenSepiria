# 우주 속성 이펙트 중 코드로 그리는 것들: 참월(cut_black, cut_white, needle, ring), 섬멸·명멸의 창(spear_blue, spear_red).
# 프레임을 가로로 이어 붙인 한 줄로 저장합니다. (void.png, ignorance.png 는 이미지 생성 API 로 만든 뒤 fx_slice.py 로 잘랐습니다.)
from PIL import Image
import random, sys
out=sys.argv[1]
W=(255,255,255,255); G=(214,220,236,255)

# 참월: 화면을 가르는 직선. 한 줄로 그어졌다가 세 줄로 갈라져 벌어지고, 가는 선을 거쳐 점선으로 끊기며 사라집니다.
# core 는 가운데 심, rim 은 테(곁줄의 밝은 쪽), fade 는 사라질 때의 색입니다.
def cut(name,core,rim,fade):
    fw,fh,n=96,16,12; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    spread=(0,0,0,2,4,5,6,6,7,7,7,7)              # 곁줄이 가운데에서 떨어진 거리(프레임별)
    for f in range(n):
        for x in range(fw):
            X=f*fw+x
            def line(y,half,a=255,solid=True):
                # 심(half*2+1 픽셀) + 위아래 테 1픽셀
                if not solid and x%((6,8,10,12)[min(3,f-8)])>=(4,4,3,2)[min(3,f-8)]: return
                for dy in range(-half,half+1):
                    if 0<=y+dy<fh: px[X,y+dy]=core[:3]+(a,)
                for dy in (-half-1,half+1):
                    if 0<=y+dy<fh: px[X,y+dy]=rim[:3]+(a,)
            if f<3:                                # 한 줄: 굵은 심
                line(cy,2 if f else 3)
            elif f<8:                              # 세 줄로 갈라짐: 가운데는 가늘어지고 곁줄이 벌어짐
                a=(255,255,235,210,180)[f-3]
                line(cy,1 if f<6 else 0,a)
                for side in (-1,1): line(cy+side*spread[f],0,a)
            else:                                  # 점선으로 끊기며 사라짐
                a=(150,115,80,45)[f-8]
                if x%((6,8,10,12)[f-8])<(4,4,3,2)[f-8]:
                    px[X,cy]=fade[:3]+(a,)
                    for side in (-1,1):
                        if (x//3+f)%2==0: px[X,cy+side*spread[f]]=fade[:3]+(a*2//3,)
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

# 참월 '흑'의 파열: 검은 바늘(가장자리에 옅은 보랏빛 테)과 그 주변에 흩날리는 별가루.
def needle_black():
    fw,fh,n=96,16,6; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2; rnd=random.Random(9)
    half=(4,3,2,1,0,0); alpha=(255,255,220,170,130,70)
    K=(14,10,24); rim=(150,120,230)
    dust=[(rnd.randrange(4,92),cy+rnd.choice((-1,1))*rnd.randrange(2,8),rnd.random()) for _ in range(26)]
    for f in range(n):
        for x in range(fw):
            t=x/(fw-1); k=1-abs(2*t-1)**1.6
            h=int(round(half[f]*k))
            if f>=4 and x%(8 if f==4 else 12)>=(5 if f==4 else 3): continue
            for y in range(cy-h,cy+h+1): px[f*fw+x,y]=K+(alpha[f],)
            if h>=1 and f<4:
                px[f*fw+x,cy-h-1]=rim+(alpha[f]*2//3,); px[f*fw+x,cy+h+1]=rim+(alpha[f]*2//3,)
        for (x,y,seed) in dust:                         # 별가루: 프레임마다 조금씩 퍼지고 깜박이다 사라집니다
            yy=y+(1 if y>cy else -1)*(f//2)
            if not (0<=yy<fh) or (int(seed*7)+f)%3==0 or f>4+seed: continue
            c=(255,255,255) if seed>0.55 else (196,170,255)
            px[f*fw+x,yy]=c+((255,255,230,190,130,70)[f],)
            if seed>0.86 and f<4:                       # 큰 별은 십자로
                for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
                    if 0<=x+dx<fw and 0<=yy+dy<fh: px[f*fw+x+dx,yy+dy]=c+(150,)
    sheet.save(out+'needle_black.png')

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

# 섬멸·명멸의 투사체: 창 모양이 아니라 빛줄기. 가늘고 흰 심이 앞(오른쪽)으로 뾰족하게 빠지고 머리에서 가장 밝게 빛나며,
# 양옆에 1픽셀짜리 가는 결이 듬성듬성 붙어 프레임마다 일렁입니다.
def spear(name,pale,light,accent,dark):
    fw,fh,n=80,16,3; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2
    Wh=(255,255,255,255)
    for f in range(n):
        rnd=random.Random(31+f)
        def put(x,y,c):
            if 0<=x<fw and 0<=y<fh: px[f*fw+x,y]=c
        # 가는 결: 가운데에서 멀수록 짧고 옅게
        for side in (-1,1):
            for k in range(1,4):                         # 결 사이를 한 줄씩 띄워 가늘게 보이게 합니다
                dy=side*(k*2+1)
                L=int((50-k*11)*rnd.uniform(0.7,1.1)); x0=rnd.randrange(6,24)+k*5
                color=(pale,light,accent)[k-1]
                if rnd.random()<0.15: continue
                for x in range(x0,x0+L): put(x,cy+dy,color[:3]+((240,200,150)[k-1],))
                gap=rnd.randrange(2,4)                   # 떨어져 나간 조각
                for x in range(x0-gap*3,x0-gap): put(x,cy+dy,accent[:3]+(120,))
        # 심: 뒤는 가늘게 사라지고, 머리 쪽은 3픽셀, 맨 앞은 1픽셀로 뾰족
        for x in range(8,fw-1):
            t=(x-8)/(fw-10)
            h=0 if t<0.35 or t>0.93 else 1
            for y in range(cy-h,cy+h+1): put(x,y,Wh if t>0.2 else pale)
        # 머리의 빛: 작은 마름모
        hx=fw-12
        for dx in range(-4,5):
            for dy in range(-3,4):
                if abs(dx)/4+abs(dy)/3<=1.0: put(hx+dx,cy+dy,Wh)
                elif abs(dx)/5+abs(dy)/4<=1.0: put(hx+dx,cy+dy,pale)
    sheet.save(out+name+'.png')

# 창이 지나간 자리: 가는 줄기 몇 가닥과 십자 반짝임이 옅어지며 사라집니다(흰색으로 그리고 게임에서 색을 입힙니다).
def spear_trail():
    fw,fh,n=80,16,4; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2; rnd=random.Random(5)
    lines=[(rnd.randrange(0,44),rnd.randrange(16,36),cy+rnd.randrange(-5,6)) for _ in range(7)]
    stars=[(rnd.randrange(6,74),cy+rnd.randrange(-6,7)) for _ in range(6)]
    for f in range(n):
        a=(230,170,110,55)[f]
        for (x0,L,y) in lines[:7-f*2]:
            for x in range(x0+f*3,min(fw,x0+L-f*2)): px[f*fw+x,y]=(255,255,255,a)
        for (x,y) in stars[f:]:
            for dx,dy in ((0,0),(1,0),(-1,0),(0,1),(0,-1)):
                if 0<=x+dx<fw and 0<=y+dy<fh: px[f*fw+x+dx,y+dy]=(255,255,255,min(255,a+25))
    sheet.save(out+'spear_trail.png')

cut('cut_black',(14,10,24,255),(255,255,255,255),(236,236,246,255))
cut('cut_white',(255,255,255,255),(151,199,254,255),(190,214,255,255))
needle()
needle_black()
ring()
spear('spear_blue',(222,240,255,255),(176,218,255,255),(120,180,250,255),(26,44,104,255))
spear('spear_red',(255,240,190,255),(255,178,90,255),(232,96,52,255),(96,24,28,255))
spear_trail()
