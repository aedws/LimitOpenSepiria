# 우주 속성 이펙트 중 코드로 그리는 것들: 참월의 베기(slash), 섬멸·명멸의 창(spear_blue, spear_red).
# 프레임을 가로로 이어 붙인 한 줄로 저장합니다. (void.png, ignorance.png 는 이미지 생성 API 로 만든 뒤 fx_slice.py 로 잘랐습니다.)
from PIL import Image
import random, sys
out=sys.argv[1]
W=(255,255,255,255); G=(214,220,236,255)

def slash():
    fw,fh,n=96,16,5; sheet=Image.new('RGBA',(fw*n,fh),(0,0,0,0)); px=sheet.load(); cy=fh//2; rnd=random.Random(7)
    def lens(f,half,x0=2,x1=fw-3,color=W):
        for x in range(x0,x1+1):
            t=(x-x0)/(x1-x0); k=1-abs(2*t-1)**2.2          # 양 끝이 뾰족한 렌즈 모양
            h=int(round(half*k))
            for y in range(cy-h,cy+h+1): px[f*fw+x,y]=color
    lens(0,0)                                              # 1: 가는 선
    lens(1,2); lens(1,0,0,fw-1)                            # 2: 굵은 칼날
    lens(2,3); lens(2,0,0,fw-1)                            # 3: 가장 굵고, 가장자리에 파편
    for _ in range(26):
        x=rnd.randrange(6,fw-6); y=cy+rnd.choice((-5,-4,4,5)); L=rnd.randrange(2,6)
        for i in range(L): px[2*fw+min(fw-1,x+i),y]=G
    for x in range(4,fw-4):                                # 4: 두 줄로 갈라지며 끊김
        if (x//7)%3!=2: px[3*fw+x,cy-1]=W
        if (x//5)%4!=1: px[3*fw+x,cy+2]=G
    for _ in range(14):                                    # 5: 흩어지는 조각
        x=rnd.randrange(4,fw-8); y=cy+rnd.randrange(-3,4); L=rnd.randrange(2,5)
        for i in range(L): px[4*fw+x+i,y]=G if rnd.random()<0.6 else W
    sheet.save(out+'slash.png')

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

slash()
spear('spear_blue',((20,30,70,255),(59,81,130,255),(100,153,232,255),(170,226,255,255),(255,255,255,255)))
spear('spear_red',((70,20,24,255),(183,50,39,255),(237,120,46,255),(255,214,110,255),(255,255,255,255)))
