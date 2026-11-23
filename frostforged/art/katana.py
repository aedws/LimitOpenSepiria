from PIL import Image
W,H=10,29
O=(18,20,40,255); HI=(255,255,255,255); L1=(203,227,254,255); L2=(151,199,254,255); B1=(100,153,232,255); B2=(59,81,130,255)
G=(226,190,104,255); GD=(168,128,66,255); HD=(45,58,103,255); HL=(52,114,203,255)
SC=(45,58,103,255); SL=(49,78,141,255); SH=(62,135,225,255)
HANDLE=5                                   # 손잡이 줄 수
POMMEL=27; H1=26; H0=H1-HANDLE+1; GUARD=H0-1; HABAKI=GUARD-1; BLADE_END=HABAKI-1
SHIFT=GUARD-17
def col(y):
    if 16<=y<=GUARD: return 4              # 늘어난 도신은 직선
    yy=y if y<16 else y-SHIFT              # 손잡이는 호를 이어서
    return int(round(4.2-0.0125*(yy-17)**2))
def outline(body,w=W,h=H):
    im=Image.new('RGBA',(w,h),(0,0,0,0)); px=im.load()
    for (x,y),c in body.items(): px[x,y]=c
    for (x,y) in body:
        for dx,dy in((1,0),(-1,0),(0,1),(0,-1)):
            nx,ny=x+dx,y+dy
            if 0<=nx<w and 0<=ny<h and (nx,ny) not in body: px[nx,ny]=O
    return im
def hilt(b):
    c=col(HABAKI); b[(c,HABAKI)]=G; b[(c+1,HABAKI)]=GD
    for x in range(c-1,c+3): b[(x,GUARD)]=G if x<c+1 else GD
    for y in range(H0,H1+1):
        c=col(y); b[(c,y)]=HL if y%2==0 else HD; b[(c+1,y)]=HD if y%2==0 else B2
    c=col(POMMEL); b[(c,POMMEL)]=G; b[(c+1,POMMEL)]=GD
def drawn():
    b={}
    for y in range(1,BLADE_END+1):
        c=col(y)
        if y<=2: b[(c,y)]=HI
        else:
            b[(c,y)]=HI if y<13 else L1
            b[(c+1,y)]=L2 if y<8 else (B1 if y<15 else B2)
    hilt(b); return outline(b)
def sheath(b,end):
    for y in range(1,end+1):
        c=col(y)
        if y==1: b[(c,y)]=SL
        else: b[(c,y)]=SL; b[(c+1,y)]=SC
        if y in (3,end-3):
            for x in (c,c+1):
                if (x,y) in b: b[(x,y)]=G if x==c else GD
        if y==10: b[(c,y)]=SH
def sheathed():
    b={}; sheath(b,BLADE_END); hilt(b); return outline(b)
def scabbard():
    b={}; sheath(b,BLADE_END); c=col(HABAKI); b[(c,HABAKI)]=G; b[(c+1,HABAKI)]=GD
    im=outline(b,W,HABAKI+2); return im.crop(im.getbbox())
import sys
d,prevpath=sys.argv[1],sys.argv[2]
shots=[]
a,s_,e=drawn(),sheathed(),scabbard()
a.save(d+'katana.png'); a.save(d+'icon.png'); s_.save(d+'katana_sheathed.png'); e.save(d+'scabbard.png')
shots+= [a,s_,e]

# ── 같은 모양, 다른 배색 ──────────────────────────────────────────────────────
# 만변도 : 초승 — 강철빛 날, 어두운 손잡이.
HI=(255,255,255,255); L1=(222,224,234,255); L2=(189,190,207,255); B1=(126,135,167,255); B2=(80,84,110,255)
G=(189,190,207,255); GD=(126,135,167,255); HD=(40,40,58,255); HL=(80,84,110,255)
SC=(40,40,58,255); SL=(57,58,81,255); SH=(126,135,167,255)
a,s_,e=drawn(),sheathed(),scabbard()
a.save(d+'katana_base.png'); s_.save(d+'katana_base_sheathed.png'); e.save(d+'scabbard_base.png')
shots+= [a,s_,e]

# 흑요도 : 참월 II형 — 칠흑의 날(날 쪽 가장자리만 옅게 빛남), 붉은 감개의 검은 손잡이, 각진 코등이, 손잡이 끝의 사슬.
HI=(150,140,176,255); L1=(84,76,110,255); L2=(44,38,62,255); B1=(30,26,44,255); B2=(22,18,34,255)
G=(126,118,156,255); GD=(64,56,90,255); HD=(34,26,40,255); HL=(183,50,39,255)
SC=(22,18,34,255); SL=(40,34,58,255); SH=(183,50,39,255)
def angular(b):
    # 각진 코등이: 코등이 줄을 좌우로 한 칸씩 넓히고, 양 끝을 위아래로 꺾습니다.
    c=col(GUARD)
    for x in (c-2,c+3): b[(x,GUARD)]=G if x<c else GD
    b[(c-2,GUARD-1)]=G; b[(c+3,GUARD+1)]=GD
def chain(b):
    # 손잡이 끝의 사슬: 고리 두 개가 비스듬히 늘어집니다.
    c=col(POMMEL)
    for (x,y),k in (((c+2,POMMEL),G),((c+3,POMMEL+1),GD)):
        if 0<=x<W and 0<=y<H: b[(x,y)]=k
_hilt=hilt
def hilt(b):
    _hilt(b); angular(b); chain(b)
a,s_,e=drawn(),sheathed(),scabbard()
a.save(d+'katana_obsidian.png'); s_.save(d+'katana_obsidian_sheathed.png'); e.save(d+'scabbard_obsidian.png')
shots+= [a,s_,e]

prev=Image.new('RGBA',(len(shots)*130,H*12+10),(70,90,90,255))
for i,im in enumerate(shots):
    b=im.resize((im.width*12,im.height*12),Image.NEAREST); prev.paste(b,(i*130+5,5),b)
prev.save(prevpath)
