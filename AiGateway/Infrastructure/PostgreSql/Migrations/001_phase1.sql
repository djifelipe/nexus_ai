CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS knowledge_module (id text PRIMARY KEY, name text NOT NULL, is_active boolean NOT NULL DEFAULT true);
CREATE TABLE IF NOT EXISTS knowledge_feature (id text PRIMARY KEY, module_id text NOT NULL REFERENCES knowledge_module(id), name text NOT NULL, is_active boolean NOT NULL DEFAULT true);
CREATE TABLE IF NOT EXISTS knowledge_action (id text PRIMARY KEY, feature_id text NOT NULL REFERENCES knowledge_feature(id), name text NOT NULL, entity_id text NULL, intent_type text NOT NULL, is_active boolean NOT NULL DEFAULT true);
CREATE TABLE IF NOT EXISTS knowledge_intent_term (
 id bigserial PRIMARY KEY, term text NOT NULL, normalized_term text NOT NULL, module_id text NOT NULL REFERENCES knowledge_module(id),
 feature_id text NULL REFERENCES knowledge_feature(id), action_id text NULL REFERENCES knowledge_action(id), entity_id text NULL,
 weight double precision NOT NULL DEFAULT 1, required_permission text NULL, is_active boolean NOT NULL DEFAULT true);
CREATE TABLE IF NOT EXISTS knowledge_source (
 id text PRIMARY KEY, company_id text NOT NULL, source_type text NOT NULL, title text NOT NULL, content text NOT NULL,
 module_id text NULL, feature_id text NULL, erp_version text NULL, language text NOT NULL DEFAULT 'pt-BR', required_permission text NULL,
 version text NULL, is_critical boolean NOT NULL DEFAULT false, is_active boolean NOT NULL DEFAULT true,
 publication_status text NOT NULL DEFAULT 'published', valid_from timestamptz NULL, valid_to timestamptz NULL, metadata jsonb NOT NULL DEFAULT '{}'::jsonb);
CREATE INDEX IF NOT EXISTS ix_knowledge_source_scope ON knowledge_source(company_id, erp_version, module_id, is_active, publication_status);
CREATE TABLE IF NOT EXISTS knowledge_chunk (id text PRIMARY KEY, source_id text NOT NULL REFERENCES knowledge_source(id) ON DELETE CASCADE, content text NOT NULL, embedding vector(768) NOT NULL, is_active boolean NOT NULL DEFAULT true);
CREATE INDEX IF NOT EXISTS ix_knowledge_chunk_source ON knowledge_chunk(source_id) WHERE is_active;
CREATE INDEX IF NOT EXISTS ix_knowledge_chunk_embedding ON knowledge_chunk USING hnsw (embedding vector_cosine_ops);

INSERT INTO knowledge_module(id,name) VALUES ('Fiscal','Fiscal'),('Financeiro','Financeiro'),('Estoque','Estoque'),('CRM','CRM') ON CONFLICT DO NOTHING;
INSERT INTO knowledge_feature(id,module_id,name) VALUES ('NFe','Fiscal','NF-e'),('ContasReceber','Financeiro','Contas a receber'),('Inventario','Estoque','Inventário') ON CONFLICT DO NOTHING;
INSERT INTO knowledge_action(id,feature_id,name,entity_id,intent_type) VALUES ('NFe.Cancelamento','NFe','Cancelamento','DocumentoFiscal','HowTo'),('ContasReceber.Cancelamento','ContasReceber','Cancelamento','TituloFinanceiro','HowTo'),('Inventario.ConsultaSaldo','Inventario','Consulta de saldo','Produto','DataQuery') ON CONFLICT DO NOTHING;
INSERT INTO knowledge_intent_term(term,normalized_term,module_id,feature_id,action_id,entity_id,weight,required_permission)
SELECT * FROM (VALUES ('NF-e','nf e','Fiscal','NFe','NFe.Cancelamento','DocumentoFiscal',1.0,'Fiscal.NFe.Visualizar'),('cancelar nota','cancelar nota','Fiscal','NFe','NFe.Cancelamento','DocumentoFiscal',1.0,'Fiscal.NFe.Visualizar'),('cancelamento','cancelamento','Fiscal','NFe','NFe.Cancelamento','DocumentoFiscal',0.7,'Fiscal.NFe.Visualizar'),('cancelamento','cancelamento','Financeiro','ContasReceber','ContasReceber.Cancelamento','TituloFinanceiro',0.7,'Financeiro.Receber.Visualizar'),('saldo','saldo','Estoque','Inventario','Inventario.ConsultaSaldo','Produto',1.0,'Estoque.Visualizar')) AS seed(term,normalized_term,module_id,feature_id,action_id,entity_id,weight,required_permission)
WHERE NOT EXISTS (SELECT 1 FROM knowledge_intent_term t WHERE t.normalized_term=seed.normalized_term AND t.action_id=seed.action_id);
