# RollRadar

Busca de vídeos de lutas de BJJ por técnica específica (ex: berimbolo, triângulo, heel hook),
com link direto para o momento em que a técnica acontece.

## Estrutura

```
backend/RollRadar.Api       API ASP.NET Core (.NET 8)
frontend/                  App Angular 22 (standalone, zoneless, testes com Vitest)
RollRadar.sln
```

## Requisitos

- .NET SDK 8
- Node.js 24 LTS (npm 11)

## Rodando localmente

Backend (http://localhost:5214, Swagger em /swagger):

```bash
dotnet run --project backend/RollRadar.Api --launch-profile http
```

Frontend (http://localhost:4200, `/api` é redirecionado para o backend via `proxy.conf.json`):

```bash
cd frontend
npm start
```

## Testes

```bash
dotnet test
```

O projeto de testes depende de uma ferramenta local que não fica neste repositório.

```bash
cd frontend
npm test
```

## Dados

A API lê tudo do PostgreSQL (`docker compose up -d`). Os dados são coletados por uma ferramenta que roda à parte e
não faz parte deste repositório.

```bash
dotnet ef database update --project backend/RollRadar.Api   # uma vez, depois de puxar uma migration nova
```

As legendas buscadas são em inglês. O catálogo de técnicas fica no banco (tabelas `techniques` e `technique_aliases`)
e permite buscar em português ou inglês:
"chave de braço", "armbar" e "armlock" viram o termo em inglês "Armbar". Maiúsculas, acentos e hífens são ignorados.
Nomes alternativos em inglês são separados por " / " (ex: `Back Control / Back Mount`).

## Endpoints

- `GET /api/videos?technique=chave de braço&athlete=meregali` — técnica em português ou inglês; retorna a técnica reconhecida,
  os termos em inglês usados e os vídeos com os momentos (segundo + trecho da legenda) em que ela é citada.
- `GET /api/videos/techniques` — técnicas do catálogo com a quantidade de vídeos de cada uma.

Esses (e os votos e a curadoria) são do site: abertos, com limite de requisições por IP (`ApiAccess:SiteRequestsPerMinute`).
Sem técnica, `/api/videos` devolve só as lutas mais recentes (`ApiAccess:UnfilteredSearchMaxVideos`, padrão 50).
A curadoria continua pedindo a chave do curador (`X-Curator-Key`).

### API de dados (com token)

Para clientes externos. Todo pedido leva `Authorization: Bearer <token>`; sem token válido a resposta é 401.
Cada token tem limite de requisições por minuto (`ApiAccess:TokenRequestsPerMinute`, padrão 60).

- `GET /api/v1/dados/techniques` — catálogo (id, nomes, categoria, termos em inglês, apelidos).
- `GET /api/v1/dados/videos?after=&limit=` — lutas, por id.
- `GET /api/v1/dados/videos/{id}/transcript` — legenda inteira da luta.
- `GET /api/v1/dados/moments?technique=&video=&status=&after=&limit=` — momentos com status da curadoria
  (`pendente`/`bom`/`ruim`) e votos.

As listas vêm em páginas (`limit` até 1000, padrão 100): passe o `next` da resposta como `after` para a próxima.
Os tokens são criados por uma ferramenta local; o banco guarda só o hash, então o token aparece uma vez só.

No Swagger, o botão "Authorize" recebe o token.

## Próximos passos

- Curadoria manual dos resultados (bom/ruim).
- Filtro por categoria de técnica (o catálogo já tem a coluna `categoria`).
